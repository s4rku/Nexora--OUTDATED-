using Microsoft.Extensions.Logging.Abstractions;
using NEXORA.Core.Hardware;
using NEXORA.Core.Models;
using Xunit;

namespace NEXORA.Tests;

public class HardwareScoringTests
{
    private readonly HardwareService _service;

    public HardwareScoringTests()
    {
        _service = new HardwareService(NullLogger<HardwareService>.Instance);
    }

    private static HardwareInfo MakeHighEndSystem() => new()
    {
        CpuName = "AMD Ryzen 9 5900X",
        CpuLogicalProcessors = 24,
        CpuBaseClockGHz = 3.7,
        TotalRamBytes = 32L * 1_073_741_824,
        RamSpeedMHz = 3600,
        Gpus = new() { new GpuInfo { IsDedicated = true, VramBytes = 8L * 1_073_741_824, Manufacturer = "NVIDIA" } },
        StorageDrives = new() { new StorageDriveInfo { Type = StorageType.NVMe, TotalBytes = 500L * 1_073_741_824, FreeBytes = 200L * 1_073_741_824 } },
        WindowsBuild = "22621"
    };

    private static HardwareInfo MakeLowEndSystem() => new()
    {
        CpuName = "Intel Core i3-4130",
        CpuLogicalProcessors = 4,
        CpuBaseClockGHz = 3.4,
        TotalRamBytes = 4L * 1_073_741_824,
        RamSpeedMHz = 1600,
        Gpus = new() { new GpuInfo { IsDedicated = false, VramBytes = 512 * 1_048_576, Manufacturer = "Intel" } },
        StorageDrives = new() { new StorageDriveInfo { Type = StorageType.HDD, TotalBytes = 500L * 1_073_741_824, FreeBytes = 5L * 1_073_741_824 } },
        WindowsBuild = "19041"
    };

    [Fact]
    public async Task HighEndSystem_ScoresHigherThan_LowEndSystem()
    {
        var highScore = await _service.CalculateScoreAsync(MakeHighEndSystem());
        var lowScore = await _service.CalculateScoreAsync(MakeLowEndSystem());

        Assert.True(highScore.Overall > lowScore.Overall,
            $"High-end score ({highScore.Overall}) should be > low-end score ({lowScore.Overall})");
    }

    [Fact]
    public async Task AllScores_AreWithinBounds()
    {
        var score = await _service.CalculateScoreAsync(MakeHighEndSystem());

        Assert.InRange(score.Overall, 0, 100);
        Assert.InRange(score.CpuScore, 0, 100);
        Assert.InRange(score.GpuScore, 0, 100);
        Assert.InRange(score.MemoryScore, 0, 100);
        Assert.InRange(score.StorageScore, 0, 100);
        Assert.InRange(score.GamingReadiness, 0, 100);
    }

    [Fact]
    public async Task LowRam_ProducesExplanation()
    {
        var score = await _service.CalculateScoreAsync(MakeLowEndSystem());
        Assert.NotEmpty(score.Explanations);
        Assert.Contains(score.Explanations, e => e.Contains("RAM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HddOnly_ProducesExplanation()
    {
        var score = await _service.CalculateScoreAsync(MakeLowEndSystem());
        Assert.Contains(score.Explanations, e =>
            e.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("load time", StringComparison.OrdinalIgnoreCase));
    }
}
