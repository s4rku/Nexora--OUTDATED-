using NEXORA.Core.Models;

namespace NEXORA.Core.Interfaces;

public interface IGameDetectionService
{
    /// <summary>Scan all supported stores and return discovered games.</summary>
    Task<List<GameEntry>> DetectGamesAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>Detect which store launcher(s) are installed.</summary>
    Task<List<GameStore>> GetInstalledStoresAsync(CancellationToken ct = default);
}
