using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NEXORA.Core.Models;

namespace NEXORA.Core.Optimization.Modules;

/// <summary>
/// Sets NVIDIA GPU power management to "Prefer Maximum Performance" via registry.
/// Only applies when an NVIDIA dedicated GPU is present.
/// This prevents the GPU from downclocking during the first few frames of a scene transition.
/// </summary>
public sealed class NvidiaPreferMaxPerformanceModule : BaseOptimizationModule
{
    // NVIDIA driver stores per-application and global power mode in:
    private const string NvKeyPath = @"SYSTEM\CurrentControlSet\Services\nvlddmkm\Global\NvTweak";
    private const string ValueName = "PowerMizerEnable";

    public NvidiaPreferMaxPerformanceModule(ILogger<NvidiaPreferMaxPerformanceModule> logger)
        : base(logger) { }

    public override string Id => "nvidia.max_performance";
    public override string Name => "NVIDIA: Prefer Maximum Performance";
    public override string Description =>
        "Sets the NVIDIA GPU power management mode to 'Prefer Maximum Performance'. " +
        "Prevents the GPU from reducing its clock speed during brief idle periods within a game, " +
        "which can cause frame time spikes.";
    public override string Category => "Gaming";
    public override RiskLevel Risk => RiskLevel.Low;
    public override string ExpectedBenefit =>
        "Eliminates GPU downclocking-induced frame time spikes. More consistent frametimes.";
    public override OptimizationLevel MinimumLevel => OptimizationLevel.Balanced;

    public override Task<bool> CanApplyAsync(HardwareInfo hardware, CancellationToken ct = default)
    {
        bool hasNvidia = hardware.Gpus.Any(g =>
            g.Manufacturer == "NVIDIA" && g.IsDedicated);
        return Task.FromResult(hasNvidia);
    }

    public override Task<string> GetCurrentValueAsync(CancellationToken ct = default)
    {
        var val = ReadRegistryDword(RegistryHive.LocalMachine, NvKeyPath, ValueName);
        return Task.FromResult(val == 1 ? "Maximum Performance" : "Adaptive (default)");
    }

    public override Task<AppliedOptimization> ApplyAsync(CancellationToken ct = default)
    {
        var prev = ReadRegistryDword(RegistryHive.LocalMachine, NvKeyPath, ValueName)?.ToString() ?? "0";
        var ok = WriteRegistryDword(RegistryHive.LocalMachine, NvKeyPath, ValueName, 1);

        return Task.FromResult(BuildRecord(prev, "1", ok,
            ok ? null : "Registry write failed — may require administrator privileges.",
            $"{{\"prev\":\"{prev}\"}}"));
    }

    public override Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        var val = ReadRegistryDword(RegistryHive.LocalMachine, NvKeyPath, ValueName);
        return Task.FromResult(val == 1);
    }

    public override Task<bool> RollbackAsync(AppliedOptimization record, CancellationToken ct = default)
    {
        int prev = int.TryParse(record.PreviousValue, out var p) ? p : 0;
        return Task.FromResult(WriteRegistryDword(RegistryHive.LocalMachine, NvKeyPath, ValueName, prev));
    }
}
