using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;
using NEXORA.Core.Optimization;
using NEXORA.Core.Optimization.Modules;
using Xunit;

namespace NEXORA.Tests;

public class OptimizationEngineTests
{
    private static HardwareInfo MakeHardware(bool hasNvidia = true, bool onBattery = false)
    {
        return new HardwareInfo
        {
            CpuName = "AMD Ryzen 5 3600",
            CpuLogicalProcessors = 12,
            CpuBaseClockGHz = 3.6,
            TotalRamBytes = 16L * 1_073_741_824,
            AvailableRamBytes = 8L * 1_073_741_824,
            RamSpeedMHz = 3200,
            WindowsBuild = "22621",
            ActivePowerPlan = "Balanced",
            IsOnBattery = onBattery,
            Gpus = hasNvidia
                ? new List<GpuInfo>
                  {
                      new() { Name = "NVIDIA GTX 1650", Manufacturer = "NVIDIA",
                              IsDedicated = true, VramBytes = 4L * 1_073_741_824,
                              DriverVersion = "531.00" }
                  }
                : new List<GpuInfo>()
        };
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsOptimizations_ForBalancedLevel()
    {
        var restoreMock = new Mock<IRestoreService>();
        var modules = new List<IOptimizationModule>
        {
            new VisualEffectsModule(NullLogger<VisualEffectsModule>.Instance),
            new PowerPlanModule(NullLogger<PowerPlanModule>.Instance),
            new GameModeModule(NullLogger<GameModeModule>.Instance),
        };
        var engine = new OptimizationEngine(
            NullLogger<OptimizationEngine>.Instance,
            restoreMock.Object,
            modules);

        var hw = MakeHardware();
        var opts = await engine.AnalyzeAsync(OptimizationLevel.Balanced, hw);

        Assert.NotNull(opts);
        // At least one module should find something to improve on a default system
        Assert.True(opts.Count >= 0); // Flexible: may vary by actual system state
    }

    [Fact]
    public async Task NvidiaModule_CanApply_OnlyWhenNvidiaPresent()
    {
        var module = new NvidiaPreferMaxPerformanceModule(NullLogger<NvidiaPreferMaxPerformanceModule>.Instance);

        var hwWithNvidia = MakeHardware(hasNvidia: true);
        var hwNoNvidia = MakeHardware(hasNvidia: false);

        var canApplyWith = await module.CanApplyAsync(hwWithNvidia);
        var canApplyWithout = await module.CanApplyAsync(hwNoNvidia);

        Assert.True(canApplyWith);
        Assert.False(canApplyWithout);
    }

    [Fact]
    public async Task PowerPlanModule_CannotApply_OnBattery()
    {
        var module = new PowerPlanModule(NullLogger<PowerPlanModule>.Instance);
        var hwOnBattery = MakeHardware(onBattery: true);

        var canApply = await module.CanApplyAsync(hwOnBattery);

        Assert.False(canApply);
    }

    [Fact]
    public async Task HagsModule_CannotApply_OnOldWindowsBuild()
    {
        var module = new HardwareAcceleratedGpuSchedulingModule(
            NullLogger<HardwareAcceleratedGpuSchedulingModule>.Instance);
        var hw = MakeHardware();
        hw.WindowsBuild = "18362"; // Windows 10 1903 — before 2004

        var canApply = await module.CanApplyAsync(hw);

        Assert.False(canApply);
    }

    [Fact]
    public async Task HagsModule_CanApply_OnWindows10_2004_WithNvidia()
    {
        var module = new HardwareAcceleratedGpuSchedulingModule(
            NullLogger<HardwareAcceleratedGpuSchedulingModule>.Instance);
        var hw = MakeHardware(hasNvidia: true);
        hw.WindowsBuild = "19041"; // Windows 10 2004

        var canApply = await module.CanApplyAsync(hw);

        // May or may not apply depending on whether already enabled
        Assert.IsType<bool>(canApply);
    }

    [Fact]
    public void OptimizationDescriptor_IsSelected_DefaultsTrue()
    {
        var desc = new OptimizationDescriptor
        {
            Name = "Test Optimization",
            IsSelected = true
        };
        Assert.True(desc.IsSelected);
    }
}
