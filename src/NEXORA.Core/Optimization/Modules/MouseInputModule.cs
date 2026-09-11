using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NEXORA.Core.Models;

namespace NEXORA.Core.Optimization.Modules;

/// <summary>
/// Disables Windows mouse pointer acceleration (Enhance Pointer Precision).
/// This makes raw mouse movement consistent and predictable in games — 
/// especially important for FPS players.
/// </summary>
public sealed class MouseInputModule : BaseOptimizationModule
{
    private const string KeyPath = @"Control Panel\Mouse";
    private const string SmoothMouseCurveOffset = "SmoothMouseCurveOffset";
    private const string MouseSpeed = "MouseSpeed";
    private const string MouseThreshold1 = "MouseThreshold1";
    private const string MouseThreshold2 = "MouseThreshold2";

    public MouseInputModule(ILogger<MouseInputModule> logger) : base(logger) { }

    public override string Id => "windows.mouse_accel";
    public override string Name => "Disable Mouse Pointer Acceleration";
    public override string Description =>
        "Disables 'Enhance Pointer Precision' (mouse acceleration) in Windows. " +
        "Mouse acceleration causes cursor movement to vary based on how fast you move the mouse, " +
        "which reduces aiming consistency in games. This change only affects games that use " +
        "raw input (most modern FPS titles).";
    public override string Category => "Gaming";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string ExpectedBenefit =>
        "More consistent and predictable mouse movement in games. Better aiming accuracy.";
    public override OptimizationLevel MinimumLevel => OptimizationLevel.Balanced;

    public override Task<bool> CanApplyAsync(HardwareInfo hardware, CancellationToken ct = default)
    {
        var speed = ReadRegistryString(RegistryHive.CurrentUser, KeyPath, MouseSpeed);
        return Task.FromResult(speed != "0");
    }

    public override Task<string> GetCurrentValueAsync(CancellationToken ct = default)
    {
        var speed = ReadRegistryString(RegistryHive.CurrentUser, KeyPath, MouseSpeed);
        return Task.FromResult(speed == "0" ? "Acceleration disabled" : "Acceleration enabled");
    }

    public override Task<AppliedOptimization> ApplyAsync(CancellationToken ct = default)
    {
        var prevSpeed = ReadRegistryString(RegistryHive.CurrentUser, KeyPath, MouseSpeed) ?? "1";
        var prevT1 = ReadRegistryString(RegistryHive.CurrentUser, KeyPath, MouseThreshold1) ?? "6";
        var prevT2 = ReadRegistryString(RegistryHive.CurrentUser, KeyPath, MouseThreshold2) ?? "10";

        bool ok = WriteRegistryString(RegistryHive.CurrentUser, KeyPath, MouseSpeed, "0")
               && WriteRegistryString(RegistryHive.CurrentUser, KeyPath, MouseThreshold1, "0")
               && WriteRegistryString(RegistryHive.CurrentUser, KeyPath, MouseThreshold2, "0");

        var rollback = Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            speed = prevSpeed,
            t1 = prevT1,
            t2 = prevT2
        });

        return Task.FromResult(BuildRecord("Enabled", "Disabled", ok,
            ok ? null : "Registry write failed.", rollback));
    }

    public override Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        var speed = ReadRegistryString(RegistryHive.CurrentUser, KeyPath, MouseSpeed);
        return Task.FromResult(speed == "0");
    }

    public override Task<bool> RollbackAsync(AppliedOptimization record, CancellationToken ct = default)
    {
        try
        {
            dynamic? data = Newtonsoft.Json.JsonConvert.DeserializeObject(record.RollbackData);
            WriteRegistryString(RegistryHive.CurrentUser, KeyPath, MouseSpeed, (string)(data?.speed ?? "1"));
            WriteRegistryString(RegistryHive.CurrentUser, KeyPath, MouseThreshold1, (string)(data?.t1 ?? "6"));
            WriteRegistryString(RegistryHive.CurrentUser, KeyPath, MouseThreshold2, (string)(data?.t2 ?? "10"));
            return Task.FromResult(true);
        }
        catch
        {
            WriteRegistryString(RegistryHive.CurrentUser, KeyPath, MouseSpeed, "1");
            WriteRegistryString(RegistryHive.CurrentUser, KeyPath, MouseThreshold1, "6");
            WriteRegistryString(RegistryHive.CurrentUser, KeyPath, MouseThreshold2, "10");
            return Task.FromResult(false);
        }
    }
}
