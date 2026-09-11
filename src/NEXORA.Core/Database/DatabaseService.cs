using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.IO;

namespace NEXORA.Core.Database;

/// <summary>
/// Manages the SQLite database that stores optimization sessions,
/// game profiles, performance snapshots, and settings.
/// </summary>
public sealed class DatabaseService : IDisposable
{
    private readonly string _dbPath;
    private readonly ILogger<DatabaseService> _logger;
    private SqliteConnection? _connection;

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "NEXORA");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "nexora.db");
    }

    public SqliteConnection GetConnection()
    {
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
        {
            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();
            // Enable WAL for better concurrent read performance
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
            cmd.ExecuteNonQuery();
        }
        return _connection;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing database at {Path}", _dbPath);
        var conn = GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = GetSchemaScript();
        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("Database initialized.");
    }

    private static string GetSchemaScript() => @"
        CREATE TABLE IF NOT EXISTS OptimizationSessions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            AppliedAt TEXT NOT NULL,
            ProfileName TEXT NOT NULL,
            Level INTEGER NOT NULL,
            IsRolledBack INTEGER NOT NULL DEFAULT 0,
            RolledBackAt TEXT,
            SnapshotBeforeJson TEXT,
            SnapshotAfterJson TEXT
        );

        CREATE TABLE IF NOT EXISTS AppliedOptimizations (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SessionId INTEGER NOT NULL REFERENCES OptimizationSessions(Id),
            OptimizationId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Category TEXT NOT NULL,
            Risk INTEGER NOT NULL,
            PreviousValue TEXT NOT NULL,
            NewValue TEXT NOT NULL,
            WasSuccessful INTEGER NOT NULL DEFAULT 1,
            ErrorMessage TEXT,
            IsRolledBack INTEGER NOT NULL DEFAULT 0,
            RollbackData TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS GameProfiles (
            Id TEXT PRIMARY KEY,
            GameId TEXT NOT NULL,
            GameName TEXT NOT NULL,
            ProfileName TEXT NOT NULL,
            FpsTarget INTEGER NOT NULL DEFAULT 2,
            CustomTargetFps INTEGER NOT NULL DEFAULT 60,
            IsActive INTEGER NOT NULL DEFAULT 0,
            PowerPlan TEXT NOT NULL DEFAULT 'High Performance',
            DisableFullscreenOptimizations INTEGER NOT NULL DEFAULT 0,
            SetHighPriority INTEGER NOT NULL DEFAULT 0,
            DisableVSync INTEGER NOT NULL DEFAULT 0,
            DisableMotionBlur INTEGER NOT NULL DEFAULT 0,
            SettingRecommendationsJson TEXT NOT NULL DEFAULT '[]',
            EstimatedFpsMin INTEGER NOT NULL DEFAULT 0,
            EstimatedFpsMax INTEGER NOT NULL DEFAULT 0,
            ConfidencePercent INTEGER NOT NULL DEFAULT 0,
            DetectedBottleneck INTEGER NOT NULL DEFAULT 0,
            BottleneckExplanation TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL,
            ModifiedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS PerformanceSnapshots (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SessionId INTEGER REFERENCES OptimizationSessions(Id),
            CapturedAt TEXT NOT NULL,
            Label TEXT NOT NULL,
            CpuUsagePercent REAL,
            GpuUsagePercent REAL,
            RamUsagePercent REAL,
            VramUsagePercent REAL,
            CpuTempCelsius REAL,
            GpuTempCelsius REAL,
            AverageFps REAL,
            OnePercentLowFps REAL,
            FrameTimeMs REAL
        );

        CREATE TABLE IF NOT EXISTS Settings (
            Key TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        );

        INSERT OR IGNORE INTO Settings (Key, Value) VALUES
            ('theme', 'dark'),
            ('telemetry_enabled', '0'),
            ('auto_game_launch', '0'),
            ('background_monitoring', '0'),
            ('show_fps_overlay', '0'),
            ('first_launch_completed', '0');
    ";

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
