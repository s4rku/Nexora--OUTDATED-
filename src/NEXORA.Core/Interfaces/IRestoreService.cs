using NEXORA.Core.Models;

namespace NEXORA.Core.Interfaces;

public interface IRestoreService
{
    /// <summary>Create a Windows system restore point with the given description.</summary>
    Task<bool> CreateSystemRestorePointAsync(string description, CancellationToken ct = default);

    /// <summary>Save an optimization session to the database.</summary>
    Task SaveSessionAsync(OptimizationSession session, CancellationToken ct = default);

    /// <summary>Retrieve all past sessions, ordered newest first.</summary>
    Task<List<OptimizationSession>> GetSessionsAsync(CancellationToken ct = default);

    /// <summary>Rollback all changes from a specific session.</summary>
    Task<bool> RollbackSessionAsync(int sessionId, CancellationToken ct = default);

    /// <summary>Rollback a single applied optimization by its record.</summary>
    Task<bool> RollbackSingleAsync(int appliedOptimizationId, CancellationToken ct = default);
}
