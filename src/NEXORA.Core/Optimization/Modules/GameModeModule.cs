using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NEXORA.Core.Models;

namespace NEXORA.Core.Optimization.Modules;

/// <summary>
/// Enables Windows Game Mode (GameBar) which instructs the OS to prioritise
/// the foreground game for CPU and GPU scheduling.
/// </summary>
public sealed class GameModeModule : BaseOptimizationModule
{
    private const string KeyPath = @"SOFTWARE\Microsoft\GameBar";
    private const string ValueName = "AllowAutoGameMode";
    private const string AutoGameMode = "AutoGameModeEnabled";

    public GameModeModule(ILogger<GameModeModule> logger) : base(logger) { }

    public override string Id => "windows.game_mode";
    public override string Name => "Enable Windows Game Mode";
    public override string Description =>
        "Enables Windows Game Mode, which signals the OS scheduler to prioritise " +
        "the foreground game for CPU and GPU resources. Also enables automatic " +
        "game mode detection.";
    public override string Category => "Gaming";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string ExpectedBenefit =>
        "Improved CPU/GPU scheduling priority for the active game. Reduced background interference.";
    public override OptimizationLevel MinimumLevel => OptimizationLevel.Safe;

    public override Task<bool> CanApplyAsync(HardwareInfo hardware, CancellationToken ct = default)
    {
        var val = ReadRegistryDword(RegistryHive.CurrentUser, KeyPath, AutoGameMode);
        return Task.FromResult(val != 1);
    }

    public override Task<string> GetCurrentValueAsync(CancellationToken ct = default)
    {
        var val = ReadRegistryDword(RegistryHive.CurrentUser, KeyPath, AutoGameMode);
        return Task.FromResult(val == 1 ? "Enabled" : "Disabled");
    }

    public override Task<AppliedOptimization> ApplyAsync(CancellationToken ct = default)
    {
        var prev = ReadRegistryDword(RegistryHive.CurrentUser, KeyPath, AutoGameMode)?.ToString() ?? "0";

        var ok1 = WriteRegistryDword(RegistryHive.CurrentUser, KeyPath, AutoGameMode, 1);
        var ok2 = WriteRegistryDword(RegistryHive.CurrentUser, KeyPath, ValueName, 1);

        var success = ok1 && ok2;
        return Task.FromResult(BuildRecord(prev, "1", success,
            success ? null : "Registry write partially failed.",
            $"{{\"prev\":\"{prev}\"}}"));
    }

    public override Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        var val = ReadRegistryDword(RegistryHive.CurrentUser, KeyPath, AutoGameMode);
        return Task.FromResult(val == 1);
    }

    public override Task<bool> RollbackAsync(AppliedOptimization record, CancellationToken ct = default)
    {
        int prevVal = int.TryParse(record.PreviousValue, out var p) ? p : 0;
        WriteRegistryDword(RegistryHive.CurrentUser, KeyPath, AutoGameMode, prevVal);
        WriteRegistryDword(RegistryHive.CurrentUser, KeyPath, ValueName, prevVal);
        return Task.FromResult(true);
    }
}
