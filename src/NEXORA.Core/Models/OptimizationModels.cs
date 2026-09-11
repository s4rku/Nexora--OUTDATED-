namespace NEXORA.Core.Models;

public enum RiskLevel { Safe, Low, Medium, High }
public enum OptimizationStatus { NotApplied, Applied, Failed, RolledBack }
public enum OptimizationLevel { Safe, Balanced, Performance, Custom }

/// <summary>
/// Describes a single optimization action with full metadata for display,
/// application, and rollback.
/// </summary>
public sealed class OptimizationDescriptor
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public RiskLevel Risk { get; set; }
    public string ExpectedBenefit { get; set; } = string.Empty;
    public OptimizationStatus Status { get; set; } = OptimizationStatus.NotApplied;
    public bool IsRecommended { get; set; }
    public bool IsSelected { get; set; }
    public OptimizationLevel MinimumLevel { get; set; } = OptimizationLevel.Safe;

    // State tracking
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? CurrentValue { get; set; }
    public bool CanRollback { get; set; } = true;
}

/// <summary>
/// Full record of an optimization session — written to the database.
/// </summary>
public sealed class OptimizationSession
{
    public int Id { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public string ProfileName { get; set; } = string.Empty;
    public OptimizationLevel Level { get; set; }
    public List<AppliedOptimization> AppliedOptimizations { get; set; } = new();
    public PerformanceSnapshot? SnapshotBefore { get; set; }
    public PerformanceSnapshot? SnapshotAfter { get; set; }
    public bool IsRolledBack { get; set; }
    public DateTime? RolledBackAt { get; set; }
}

/// <summary>
/// One applied change stored for rollback purposes.
/// </summary>
public sealed class AppliedOptimization
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string OptimizationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public RiskLevel Risk { get; set; }
    public string PreviousValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public bool WasSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsRolledBack { get; set; }
    public string RollbackData { get; set; } = string.Empty; // JSON payload for restore
}

/// <summary>
/// Point-in-time performance snapshot used for before/after comparison.
/// </summary>
public sealed class PerformanceSnapshot
{
    public int Id { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public double CpuUsagePercent { get; set; }
    public double GpuUsagePercent { get; set; }
    public double RamUsagePercent { get; set; }
    public double VramUsagePercent { get; set; }
    public double CpuTempCelsius { get; set; }
    public double GpuTempCelsius { get; set; }
    public double AverageFps { get; set; }
    public double OnePercentLowFps { get; set; }
    public double FrameTimeMs { get; set; }
    public string Label { get; set; } = string.Empty; // "Before", "After"
}
