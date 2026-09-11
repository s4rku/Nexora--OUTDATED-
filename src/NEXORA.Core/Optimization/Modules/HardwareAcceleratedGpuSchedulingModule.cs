using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NEXORA.Core.Models;

namespace NEXORA.Core.Optimization.Modules;

/// <summary>
/// Enables Hardware-Accelerated GPU Scheduling (HAGS) on Windows 10 2004+ with
/// a supported GPU (NVIDIA 10-series+, AMD RX 5000+, Intel Arc).
/// Requires a reboot to take effect.
/// </summary>
public sealed class HardwareAcceleratedGpuSchedulingModule : BaseOptimizationModule
{
    private const string KeyPath = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
    private const string ValueName = "HwSchMode";

    public HardwareAcceleratedGpuSchedulingModule(ILogger<HardwareAcceleratedGpuSchedulingModule> logger)
        : base(logger) { }

    public override string Id => "windows.hags";
    public override string Name => "Enable Hardware-Accelerated GPU Scheduling";
    public override string Description =>
        "Enables HAGS, which moves GPU scheduling work from the CPU to the GPU itself. " +
        "Reduces latency and can improve frame pacing. " +
        "Requires a compatible GPU (NVIDIA 10-series or newer, AMD RX 5000 series or newer) " +
        "and Windows 10 version 2004 or later. A system restart is required.";
    public override string Category => "Gaming";
    public override RiskLevel Risk => RiskLevel.Low;
    public override string ExpectedBenefit =>
        "Reduced GPU scheduling latency and improved frame consistency in GPU-limited scenarios.";
    public override OptimizationLevel MinimumLevel => OptimizationLevel.Balanced;

    public override Task<bool> CanApplyAsync(HardwareInfo hardware, CancellationToken ct = default)
    {
        // Check Windows build >= 19041 (2004)
        if (!int.TryParse(hardware.WindowsBuild, out var build) || build < 19041)
            return Task.FromResult(false);

        // Check for a supported dedicated GPU
        bool supportedGpu = hardware.Gpus.Any(g =>
            g.IsDedicated && (
                g.Manufacturer == "NVIDIA" ||
                g.Manufacturer == "AMD" ||
                (g.Manufacturer == "Intel" && g.Name.Contains("Arc", StringComparison.OrdinalIgnoreCase))
            ));
        if (!supportedGpu) return Task.FromResult(false);

        // Not already enabled
        var current = ReadRegistryDword(RegistryHive.LocalMachine, KeyPath, ValueName);
        return Task.FromResult(current != 2);
    }

    public override Task<string> GetCurrentValueAsync(CancellationToken ct = default)
    {
        var val = ReadRegistryDword(RegistryHive.LocalMachine, KeyPath, ValueName);
        return Task.FromResult(val == 2 ? "Enabled" : "Disabled");
    }

    public override Task<AppliedOptimization> ApplyAsync(CancellationToken ct = default)
    {
        var prev = ReadRegistryDword(RegistryHive.LocalMachine, KeyPath, ValueName)?.ToString() ?? "1";
        var success = WriteRegistryDword(RegistryHive.LocalMachine, KeyPath, ValueName, 2);

        return Task.FromResult(BuildRecord(prev, "2", success,
            success ? null : "Registry write failed — may require administrator privileges.",
            $"{{\"prev\":\"{prev}\"}}"));
    }

    public override Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        var val = ReadRegistryDword(RegistryHive.LocalMachine, KeyPath, ValueName);
        return Task.FromResult(val == 2);
    }

    public override Task<bool> RollbackAsync(AppliedOptimization record, CancellationToken ct = default)
    {
        int prev = int.TryParse(record.PreviousValue, out var p) ? p : 1;
        return Task.FromResult(WriteRegistryDword(RegistryHive.LocalMachine, KeyPath, ValueName, prev));
    }
}
