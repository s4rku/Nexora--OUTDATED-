namespace NEXORA.Core.Models;

public enum GameStore
{
    Steam, EpicGames, RiotGames, UbisoftConnect,
    EA, BattleNet, MicrosoftStore, GOG, Standalone
}

public enum BottleneckType
{
    Balanced, CpuLimited, GpuLimited, MemoryLimited,
    VramLimited, StorageLimited, ThermalThrottling, Unknown
}

public enum FpsTarget { Maximum, Competitive, Balanced, Quality, Custom }

/// <summary>
/// A detected game installation.
/// </summary>
public sealed class GameEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string InstallDirectory { get; set; } = string.Empty;
    public GameStore Store { get; set; }
    public string StoreAppId { get; set; } = string.Empty;
    public string? CoverImagePath { get; set; }
    public DateTime? LastPlayed { get; set; }
    public long InstallSizeBytes { get; set; }
    public double InstallSizeGB => InstallSizeBytes / 1_073_741_824.0;
    public string? ConfigFilePath { get; set; }
    public bool HasProfile { get; set; }
    public string? ProfileId { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Optimization profile for a specific game.
/// </summary>
public sealed class GameProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string ProfileName { get; set; } = "Default";
    public FpsTarget FpsTarget { get; set; } = FpsTarget.Balanced;
    public int CustomTargetFps { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // System-level settings applied when this game launches
    public string PowerPlan { get; set; } = "High Performance";
    public bool DisableFullscreenOptimizations { get; set; }
    public bool SetHighPriority { get; set; }
    public bool DisableVSync { get; set; }
    public bool DisableMotionBlur { get; set; }

    // In-game config recommendations (informational — displayed to user)
    public List<GameSettingRecommendation> SettingRecommendations { get; set; } = new();

    // Estimated FPS range
    public int EstimatedFpsMin { get; set; }
    public int EstimatedFpsMax { get; set; }
    public int ConfidencePercent { get; set; }

    public BottleneckType DetectedBottleneck { get; set; } = BottleneckType.Unknown;
    public string BottleneckExplanation { get; set; } = string.Empty;
}

/// <summary>
/// A single recommended in-game setting change.
/// </summary>
public sealed class GameSettingRecommendation
{
    public string SettingName { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string RecommendedValue { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ExpectedImpact { get; set; } = string.Empty; // "+5–10 FPS"
}
