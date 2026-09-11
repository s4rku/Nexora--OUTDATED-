using NEXORA.Core.Models;

namespace NEXORA.Core.Interfaces;

public interface IHardwareService
{
    /// <summary>Run a full hardware scan and return the result.</summary>
    Task<HardwareInfo> ScanAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>Get the last cached scan result without re-scanning.</summary>
    HardwareInfo? GetCachedInfo();

    /// <summary>Sample live performance metrics once.</summary>
    Task<LiveMetrics> GetLiveMetricsAsync(CancellationToken ct = default);

    /// <summary>Calculate the NEXORA Performance Score based on detected hardware.</summary>
    Task<PerformanceScore> CalculateScoreAsync(HardwareInfo hardware, CancellationToken ct = default);
}
