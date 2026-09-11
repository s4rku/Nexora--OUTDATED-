using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NEXORA.Core.Database;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;
using NEXORA.Core.Optimization;

namespace NEXORA.Core.Restore;

/// <summary>
/// Persists optimization sessions to SQLite and provides rollback capability.
/// Every change made by NEXORA can be undone through this service.
/// </summary>
public sealed class RestoreService : IRestoreService
{
    private readonly DatabaseService _db;
    private readonly ILogger<RestoreService> _logger;
    private readonly IEnumerable<IOptimizationModule> _modules;

    public RestoreService(
        DatabaseService db,
        ILogger<RestoreService> logger,
        IEnumerable<IOptimizationModule> modules)
    {
        _db = db;
        _logger = logger;
        _modules = modules;
    }

    public async Task<bool> CreateSystemRestorePointAsync(string description, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Requires elevated privileges — gracefully skips if not available
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NonInteractive -Command \"Checkpoint-Computer -Description '{description}' -RestorePointType MODIFY_SETTINGS\" 2>$null",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(30000);
                var success = proc?.ExitCode == 0;
                if (success)
                    _logger.LogInformation("System restore point created: {Desc}", description);
                else
                    _logger.LogWarning("System restore point creation failed or was skipped.");
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not create system restore point.");
                return false;
            }
        }, ct);
    }

    public async Task SaveSessionAsync(OptimizationSession session, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var transaction = await conn.BeginTransactionAsync(ct);

        try
        {
            // Insert session
            await using var sessionCmd = conn.CreateCommand();
            sessionCmd.CommandText = @"
                INSERT INTO OptimizationSessions (AppliedAt, ProfileName, Level, SnapshotBeforeJson, SnapshotAfterJson)
                VALUES (@at, @name, @level, @before, @after);
                SELECT last_insert_rowid();";
            sessionCmd.Parameters.AddWithValue("@at", session.AppliedAt.ToString("O"));
            sessionCmd.Parameters.AddWithValue("@name", session.ProfileName);
            sessionCmd.Parameters.AddWithValue("@level", (int)session.Level);
            sessionCmd.Parameters.AddWithValue("@before", session.SnapshotBefore != null ? JsonConvert.SerializeObject(session.SnapshotBefore) : DBNull.Value);
            sessionCmd.Parameters.AddWithValue("@after", session.SnapshotAfter != null ? JsonConvert.SerializeObject(session.SnapshotAfter) : DBNull.Value);
            var newId = Convert.ToInt32(await sessionCmd.ExecuteScalarAsync(ct));
            session.Id = newId;

            // Insert applied optimizations
            foreach (var opt in session.AppliedOptimizations)
            {
                await using var optCmd = conn.CreateCommand();
                optCmd.CommandText = @"
                    INSERT INTO AppliedOptimizations
                    (SessionId, OptimizationId, Name, Category, Risk, PreviousValue, NewValue,
                     WasSuccessful, ErrorMessage, RollbackData)
                    VALUES (@sid, @oid, @name, @cat, @risk, @prev, @new,
                            @ok, @err, @rb)";
                optCmd.Parameters.AddWithValue("@sid", newId);
                optCmd.Parameters.AddWithValue("@oid", opt.OptimizationId);
                optCmd.Parameters.AddWithValue("@name", opt.Name);
                optCmd.Parameters.AddWithValue("@cat", opt.Category);
                optCmd.Parameters.AddWithValue("@risk", (int)opt.Risk);
                optCmd.Parameters.AddWithValue("@prev", opt.PreviousValue);
                optCmd.Parameters.AddWithValue("@new", opt.NewValue);
                optCmd.Parameters.AddWithValue("@ok", opt.WasSuccessful ? 1 : 0);
                optCmd.Parameters.AddWithValue("@err", (object?)opt.ErrorMessage ?? DBNull.Value);
                optCmd.Parameters.AddWithValue("@rb", opt.RollbackData ?? "{}");
                await optCmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            _logger.LogInformation("Saved optimization session {Id} with {Count} changes.", newId, session.AppliedOptimizations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save optimization session.");
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<OptimizationSession>> GetSessionsAsync(CancellationToken ct = default)
    {
        var sessions = new List<OptimizationSession>();
        var conn = _db.GetConnection();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM OptimizationSessions ORDER BY Id DESC";
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var session = new OptimizationSession
            {
                Id = reader.GetInt32(0),
                AppliedAt = DateTime.Parse(reader.GetString(1)),
                ProfileName = reader.GetString(2),
                Level = (OptimizationLevel)reader.GetInt32(3),
                IsRolledBack = reader.GetInt32(4) == 1
            };

            if (!reader.IsDBNull(5))
                session.RolledBackAt = DateTime.Parse(reader.GetString(5));

            if (!reader.IsDBNull(6))
                session.SnapshotBefore = JsonConvert.DeserializeObject<PerformanceSnapshot>(reader.GetString(6));
            if (!reader.IsDBNull(7))
                session.SnapshotAfter = JsonConvert.DeserializeObject<PerformanceSnapshot>(reader.GetString(7));

            // Load applied optimizations for this session
            session.AppliedOptimizations = await GetAppliedOptimizationsAsync(session.Id, ct);
            sessions.Add(session);
        }

        return sessions;
    }

    private async Task<List<AppliedOptimization>> GetAppliedOptimizationsAsync(int sessionId, CancellationToken ct)
    {
        var result = new List<AppliedOptimization>();
        var conn = _db.GetConnection();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM AppliedOptimizations WHERE SessionId = @sid ORDER BY Id";
        cmd.Parameters.AddWithValue("@sid", sessionId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            result.Add(new AppliedOptimization
            {
                Id = reader.GetInt32(0),
                SessionId = reader.GetInt32(1),
                OptimizationId = reader.GetString(2),
                Name = reader.GetString(3),
                Category = reader.GetString(4),
                Risk = (RiskLevel)reader.GetInt32(5),
                PreviousValue = reader.GetString(6),
                NewValue = reader.GetString(7),
                WasSuccessful = reader.GetInt32(8) == 1,
                ErrorMessage = reader.IsDBNull(9) ? null : reader.GetString(9),
                IsRolledBack = reader.GetInt32(10) == 1,
                RollbackData = reader.GetString(11)
            });
        }
        return result;
    }

    public async Task<bool> RollbackSessionAsync(int sessionId, CancellationToken ct = default)
    {
        _logger.LogInformation("Rolling back session {Id}…", sessionId);

        var sessions = await GetSessionsAsync(ct);
        var session = sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session == null)
        {
            _logger.LogWarning("Session {Id} not found.", sessionId);
            return false;
        }

        bool allSuccess = true;

        // Roll back in reverse order
        foreach (var opt in Enumerable.Reverse(session.AppliedOptimizations))
        {
            if (opt.IsRolledBack || !opt.WasSuccessful) continue;

            var module = _modules.FirstOrDefault(m => m.Id == opt.OptimizationId);
            if (module == null)
            {
                _logger.LogWarning("Module {Id} not found for rollback.", opt.OptimizationId);
                allSuccess = false;
                continue;
            }

            try
            {
                var ok = await module.RollbackAsync(opt, ct);
                if (ok)
                    await MarkOptimizationRolledBackAsync(opt.Id, ct);
                else
                    allSuccess = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rollback failed for {Name}.", opt.Name);
                allSuccess = false;
            }
        }

        if (allSuccess)
            await MarkSessionRolledBackAsync(sessionId, ct);

        return allSuccess;
    }

    public async Task<bool> RollbackSingleAsync(int appliedOptimizationId, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM AppliedOptimizations WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", appliedOptimizationId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct)) return false;

        var opt = new AppliedOptimization
        {
            Id = reader.GetInt32(0),
            OptimizationId = reader.GetString(2),
            Name = reader.GetString(3),
            Category = reader.GetString(4),
            Risk = (RiskLevel)reader.GetInt32(5),
            PreviousValue = reader.GetString(6),
            NewValue = reader.GetString(7),
            RollbackData = reader.GetString(11)
        };

        var module = _modules.FirstOrDefault(m => m.Id == opt.OptimizationId);
        if (module == null) return false;

        var success = await module.RollbackAsync(opt, ct);
        if (success) await MarkOptimizationRolledBackAsync(opt.Id, ct);
        return success;
    }

    private async Task MarkOptimizationRolledBackAsync(int id, CancellationToken ct)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE AppliedOptimizations SET IsRolledBack = 1 WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task MarkSessionRolledBackAsync(int id, CancellationToken ct)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE OptimizationSessions SET IsRolledBack = 1, RolledBackAt = @at WHERE Id = @id";
        cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
