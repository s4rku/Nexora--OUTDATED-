using NEXORA.Core.Models;

namespace NEXORA.Core.Interfaces;

public interface IOptimizationEngine
{
    /// <summary>
    /// Analyze the system and return the list of recommended optimizations
    /// at the given level, without applying anything.
    /// </summary>
    Task<List<OptimizationDescriptor>> AnalyzeAsync(
        OptimizationLevel level,
        HardwareInfo hardware,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Apply the provided list of optimization descriptors.
    /// Returns a completed OptimizationSession with per-item results.
    /// </summary>
    Task<OptimizationSession> ApplyAsync(
        List<OptimizationDescriptor> selected,
        OptimizationLevel level,
        IProgress<(string message, int percent)>? progress = null,
        CancellationToken ct = default);

    /// <summary>All registered optimization modules.</summary>
    IReadOnlyList<IOptimizationModule> AllModules { get; }
}
