using NEXORA.Core.Models;

namespace NEXORA.Core.Interfaces;

/// <summary>
/// Contract that every optimization module must implement.
/// Ensures the DETECT → ANALYZE → RECOMMEND → OPTIMIZE → MEASURE → VERIFY → ROLLBACK
/// lifecycle is respected by all modules.
/// </summary>
public interface IOptimizationModule
{
    /// <summary>Unique stable identifier for this module.</summary>
    string Id { get; }

    /// <summary>Human-readable name shown in the UI.</summary>
    string Name { get; }

    /// <summary>Full explanation of what this module changes.</summary>
    string Description { get; }

    /// <summary>Category for grouping in the UI (Windows, Gaming, System, etc.).</summary>
    string Category { get; }

    /// <summary>Risk level of the change.</summary>
    RiskLevel Risk { get; }

    /// <summary>Plain-language description of the expected benefit.</summary>
    string ExpectedBenefit { get; }

    /// <summary>
    /// Minimum optimization level at which this module is automatically selected.
    /// </summary>
    OptimizationLevel MinimumLevel { get; }

    /// <summary>
    /// Inspect the current system state and determine whether this optimization
    /// is applicable, beneficial, and safe to apply.
    /// Returns false if the optimization should not be offered (already optimal,
    /// not relevant to this hardware, etc.)
    /// </summary>
    Task<bool> CanApplyAsync(HardwareInfo hardware, CancellationToken ct = default);

    /// <summary>
    /// Read and return the current system value this module would change.
    /// Used to populate "Current State" in the UI before applying.
    /// </summary>
    Task<string> GetCurrentValueAsync(CancellationToken ct = default);

    /// <summary>
    /// Apply the optimization.
    /// Must save previous state internally so Rollback() can restore it.
    /// Returns an AppliedOptimization record for audit trail storage.
    /// </summary>
    Task<AppliedOptimization> ApplyAsync(CancellationToken ct = default);

    /// <summary>
    /// Validate that the optimization was successfully applied.
    /// Called automatically after Apply().
    /// </summary>
    Task<bool> ValidateAsync(CancellationToken ct = default);

    /// <summary>
    /// Restore the system to exactly the state it was in before Apply().
    /// Must not throw if nothing was applied.
    /// </summary>
    Task<bool> RollbackAsync(AppliedOptimization record, CancellationToken ct = default);
}
