using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NEXORA.Core.Models;

namespace NEXORA.Core.Optimization.Modules;

/// <summary>
/// Adjusts Windows visual effects to favour performance.
/// Sets the VisualFXSetting in the registry to "Best Performance for Programs" (value 3)
/// while preserving font smoothing (ClearType) so text remains readable.
/// </summary>
public sealed class VisualEffectsModule : BaseOptimizationModule
{
    private const string KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
    private const string ValueName = "VisualFXSetting";

    public VisualEffectsModule(ILogger<VisualEffectsModule> logger) : base(logger) { }

    public override string Id => "windows.visual_effects";
    public override string Name => "Optimise Visual Effects";
    public override string Description =>
        "Switches Windows visual effects to 'Adjust for best performance of programs' (value 3). " +
        "Removes window animations, shadow effects, and transparency that consume GPU resources " +
        "but keeps font smoothing so text remains clear.";
    public override string Category => "Windows";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string ExpectedBenefit => "Reduces GPU overhead from desktop rendering, freeing resources for games.";
    public override OptimizationLevel MinimumLevel => OptimizationLevel.Safe;

    public override Task<bool> CanApplyAsync(HardwareInfo hardware, CancellationToken ct = default)
    {
        var current = ReadRegistryDword(RegistryHive.CurrentUser, KeyPath, ValueName);
        // Only recommend if not already set to performance (3)
        return Task.FromResult(current != 3);
    }

    public override Task<string> GetCurrentValueAsync(CancellationToken ct = default)
    {
        var val = ReadRegistryDword(RegistryHive.CurrentUser, KeyPath, ValueName);
        return Task.FromResult(val switch
        {
            0 => "Let Windows decide",
            1 => "Best appearance",
            2 => "Custom",
            3 => "Best performance",
            _ => $"Unknown ({val})"
        });
    }

    public override Task<AppliedOptimization> ApplyAsync(CancellationToken ct = default)
    {
        var previous = ReadRegistryDword(RegistryHive.CurrentUser, KeyPath, ValueName)?.ToString() ?? "unknown";
        var success = WriteRegistryDword(RegistryHive.CurrentUser, KeyPath, ValueName, 3);

        if (success)
            Logger.LogInformation("Visual effects set to Best Performance (3). Previous: {Prev}", previous);
        else
            Logger.LogWarning("Failed to set visual effects registry value.");

        return Task.FromResult(BuildRecord(previous, "3", success,
            success ? null : "Registry write failed.",
            $"{{\"hive\":\"HKCU\",\"key\":\"{KeyPath}\",\"name\":\"{ValueName}\",\"prev\":\"{previous}\"}}"));
    }

    public override Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        var val = ReadRegistryDword(RegistryHive.CurrentUser, KeyPath, ValueName);
        return Task.FromResult(val == 3);
    }

    public override Task<bool> RollbackAsync(AppliedOptimization record, CancellationToken ct = default)
    {
        if (int.TryParse(record.PreviousValue, out var prev))
            return Task.FromResult(WriteRegistryDword(RegistryHive.CurrentUser, KeyPath, ValueName, prev));

        // Default safe fallback: Balanced (0)
        return Task.FromResult(WriteRegistryDword(RegistryHive.CurrentUser, KeyPath, ValueName, 0));
    }
}
