using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;
using System.Collections.ObjectModel;

namespace NEXORA.ViewModels;

public sealed partial class GameOptimizerViewModel : ObservableObject
{
    private readonly IHardwareService _hardware;
    private readonly IGameDetectionService _detection;

    [ObservableProperty] private GameEntry? _selectedGame;
    [ObservableProperty] private GameProfile? _activeProfile;
    [ObservableProperty] private FpsTarget _fpsTarget = FpsTarget.Balanced;
    [ObservableProperty] private bool _isAnalyzing;
    [ObservableProperty] private string _statusMessage = "Select a game to begin.";
    [ObservableProperty] private HardwareInfo? _hardwareInfo;
    [ObservableProperty] private string _bottleneckLabel = "—";
    [ObservableProperty] private string _bottleneckExplanation = "Select a game and run analysis.";
    [ObservableProperty] private string _estimatedFpsRange = "—";

    public ObservableCollection<GameEntry> Games { get; } = new();
    public ObservableCollection<GameSettingRecommendation> Recommendations { get; } = new();

    public GameOptimizerViewModel(IHardwareService hardware, IGameDetectionService detection)
    {
        _hardware = hardware;
        _detection = detection;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        HardwareInfo = _hardware.GetCachedInfo() ?? await _hardware.ScanAsync();
        var games = await _detection.DetectGamesAsync();
        Games.Clear();
        foreach (var g in games.OrderBy(g => g.Name))
            Games.Add(g);
    }
    [RelayCommand]
    public Task AnalyzeGameAsync()
    {
        if (SelectedGame == null || HardwareInfo == null)
        {
            StatusMessage = "Select a game first.";
            return Task.CompletedTask;
        }

        IsAnalyzing = true;
        StatusMessage = $"Analysing {SelectedGame.Name}…";

        try
        {
            var bottleneck = DetectBottleneck(HardwareInfo);
            BottleneckLabel = bottleneck.ToString().ToUpperInvariant().Replace("LIMITED", " LIMITED");
            BottleneckExplanation = BuildBottleneckExplanation(bottleneck, HardwareInfo);

            ActiveProfile = BuildProfile(SelectedGame, HardwareInfo, bottleneck, FpsTarget);
            EstimatedFpsRange = $"~{ActiveProfile.EstimatedFpsMin}–{ActiveProfile.EstimatedFpsMax} FPS "
                              + $"(Confidence: {ActiveProfile.ConfidencePercent}%)";

            Recommendations.Clear();
            foreach (var r in ActiveProfile.SettingRecommendations)
                Recommendations.Add(r);

            StatusMessage = "Analysis complete. Review recommendations below.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
        }
        return Task.CompletedTask;
    }

    private static BottleneckType DetectBottleneck(HardwareInfo hw)
    {
        var gpu = hw.PrimaryGpu;
        if (gpu == null || !gpu.IsDedicated) return BottleneckType.GpuLimited;
        if (hw.TotalRamGB < 8) return BottleneckType.MemoryLimited;
        if (gpu.VramGB < 2) return BottleneckType.VramLimited;
        if (hw.CpuLogicalProcessors < 4) return BottleneckType.CpuLimited;
        return BottleneckType.Balanced;
    }

    private static string BuildBottleneckExplanation(BottleneckType type, HardwareInfo hw)
    {
        var gpu = hw.PrimaryGpu;
        return type switch
        {
            BottleneckType.GpuLimited =>
                $"Your GPU ({gpu?.Name ?? "Unknown"}) is likely the primary performance limit. "
              + "Lowering graphics settings will provide the largest FPS improvement.",
            BottleneckType.CpuLimited =>
                $"Your CPU ({hw.CpuName}) may limit performance in CPU-heavy scenarios. "
              + "Game physics, large player counts, and high draw call games are most affected.",
            BottleneckType.MemoryLimited =>
                $"With {hw.TotalRamGB:F0} GB RAM, some modern games may use the page file, "
              + "causing stuttering. 16 GB is recommended.",
            BottleneckType.VramLimited =>
                $"Your GPU has {gpu?.VramGB:F1} GB VRAM, which may be insufficient at "
              + "high resolutions or with high-resolution textures.",
            BottleneckType.Balanced =>
                "Your hardware appears reasonably balanced for gaming. "
              + "No single major bottleneck was identified.",
            _ => "Analysis inconclusive. Run with a game open for accurate bottleneck detection."
        };
    }

    private static GameProfile BuildProfile(GameEntry game, HardwareInfo hw,
        BottleneckType bottleneck, FpsTarget target)
    {
        var profile = new GameProfile
        {
            GameId = game.Id,
            GameName = game.Name,
            ProfileName = target.ToString(),
            FpsTarget = target,
            DetectedBottleneck = bottleneck,
            BottleneckExplanation = BuildBottleneckExplanation(bottleneck, hw),
            SetHighPriority = true,
            DisableFullscreenOptimizations = false,
            PowerPlan = "High Performance"
        };

        // Build setting recommendations based on hardware + bottleneck
        var recs = new List<GameSettingRecommendation>();
        var gpu = hw.PrimaryGpu;

        if (bottleneck == BottleneckType.GpuLimited || bottleneck == BottleneckType.VramLimited)
        {
            recs.Add(new GameSettingRecommendation
            {
                SettingName = "Shadows",
                RecommendedValue = target == FpsTarget.Competitive ? "Low" : "Medium",
                Reason = "Shadows are among the highest GPU-cost settings.",
                ExpectedImpact = "+5–15 FPS"
            });
            recs.Add(new GameSettingRecommendation
            {
                SettingName = "Anti-Aliasing",
                RecommendedValue = target == FpsTarget.Maximum ? "Off" : "FXAA",
                Reason = "MSAA is expensive on the GPU. FXAA has minimal performance cost.",
                ExpectedImpact = "+3–8 FPS"
            });
            recs.Add(new GameSettingRecommendation
            {
                SettingName = "Ambient Occlusion",
                RecommendedValue = target == FpsTarget.Competitive ? "Off" : "SSAO",
                Reason = "Ambient occlusion has a significant GPU cost.",
                ExpectedImpact = "+2–6 FPS"
            });
        }

        if (bottleneck == BottleneckType.VramLimited && gpu != null && gpu.VramGB <= 4)
        {
            recs.Add(new GameSettingRecommendation
            {
                SettingName = "Texture Quality",
                RecommendedValue = "Medium",
                Reason = $"Your GPU has {gpu.VramGB:F1} GB VRAM. High textures may exceed available VRAM.",
                ExpectedImpact = "Prevents VRAM overflow stuttering"
            });
        }

        recs.Add(new GameSettingRecommendation
        {
            SettingName = "VSync",
            RecommendedValue = "Off",
            Reason = "VSync caps FPS to monitor refresh rate and adds input latency.",
            ExpectedImpact = "Reduced input latency"
        });

        recs.Add(new GameSettingRecommendation
        {
            SettingName = "Motion Blur",
            RecommendedValue = "Off",
            Reason = "Motion blur adds a post-processing pass with no gameplay benefit.",
            ExpectedImpact = "+1–3 FPS, cleaner image"
        });

        profile.SettingRecommendations = recs;

        // FPS estimate (heuristic — not a guarantee)
        (profile.EstimatedFpsMin, profile.EstimatedFpsMax, profile.ConfidencePercent) =
            EstimateFps(hw, bottleneck, target);

        return profile;
    }

    private static (int min, int max, int confidence) EstimateFps(
        HardwareInfo hw, BottleneckType bottleneck, FpsTarget target)
    {
        var gpu = hw.PrimaryGpu;
        if (gpu == null) return (20, 40, 30);

        // Very rough tier-based estimate — this is displayed with explicit uncertainty
        var baseFps = gpu.VramGB switch
        {
            >= 12 => 140,
            >= 8 => 100,
            >= 6 => 75,
            >= 4 => 55,
            >= 2 => 38,
            _ => 25
        };

        if (!gpu.IsDedicated) baseFps = 30;
        if (hw.CpuLogicalProcessors < 4) baseFps = (int)(baseFps * 0.7);
        if (bottleneck == BottleneckType.VramLimited) baseFps = (int)(baseFps * 0.75);

        var range = target switch
        {
            FpsTarget.Competitive => (int)(baseFps * 0.9),
            FpsTarget.Balanced => (int)(baseFps * 0.75),
            FpsTarget.Quality => (int)(baseFps * 0.6),
            _ => (int)(baseFps * 0.75)
        };

        return (Math.Max(range - 15, 10), range + 15, 45);
    }
}
