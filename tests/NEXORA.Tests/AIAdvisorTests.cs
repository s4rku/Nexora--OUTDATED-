using NEXORA.Core.AI;
using NEXORA.Core.Models;
using Xunit;

namespace NEXORA.Tests;

public class AIAdvisorTests
{
    private static HardwareInfo MakeHardware() => new()
    {
        CpuName = "Intel Core i5-10400",
        CpuLogicalProcessors = 12,
        TotalRamBytes = 8L * 1_073_741_824,
        AvailableRamBytes = 2L * 1_073_741_824,
        Gpus = new List<GpuInfo>
        {
            new() { Name = "NVIDIA GTX 1060 6GB", Manufacturer = "NVIDIA",
                    IsDedicated = true, VramBytes = 6L * 1_073_741_824 }
        },
        Displays = new List<DisplayInfo>
        {
            new() { ResolutionWidth = 1920, ResolutionHeight = 1080, RefreshRateHz = 144, IsPrimary = true }
        }
    };

    private static LiveMetrics MakeMetrics(double cpu = 50, double gpu = 95, double ram = 70)
        => new() { CpuUsagePercent = cpu, GpuUsagePercent = gpu, RamUsagePercent = ram,
                   GpuTempCelsius = 75, CpuTempCelsius = 65 };

    [Fact]
    public void AnalyzePerformance_DetectsGpuBottleneck()
    {
        var advisor = new NexoraAIAdvisor();
        var metrics = MakeMetrics(cpu: 45, gpu: 96);
        var hw = MakeHardware();

        var result = advisor.AnalyzePerformance(metrics, hw);

        Assert.Equal(BottleneckType.GpuLimited, result.BottleneckType);
        Assert.NotEmpty(result.PrimaryInsight);
        Assert.NotEmpty(result.Recommendations);
    }

    [Fact]
    public void AnalyzePerformance_DetectsThermalThrottling()
    {
        var advisor = new NexoraAIAdvisor();
        var metrics = new LiveMetrics { CpuTempCelsius = 95, GpuTempCelsius = 90 };
        var hw = MakeHardware();

        var result = advisor.AnalyzePerformance(metrics, hw);

        Assert.Equal(BottleneckType.ThermalThrottling, result.BottleneckType);
        Assert.Contains("thermal", result.PrimaryInsight, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnswerQuestion_FpsQuestion_ReturnsNonEmpty()
    {
        var advisor = new NexoraAIAdvisor();
        var result = advisor.AnswerQuestion("Why is my FPS low?", MakeMetrics(), MakeHardware());
        Assert.NotEmpty(result);
    }

    [Fact]
    public void AnswerQuestion_TemperatureQuestion_IncludesValues()
    {
        var advisor = new NexoraAIAdvisor();
        var metrics = MakeMetrics(); // 65°C CPU, 75°C GPU
        var result = advisor.AnswerQuestion("What are my temperatures?", metrics, MakeHardware());
        Assert.Contains("65", result);
        Assert.Contains("75", result);
    }

    [Fact]
    public void AnswerQuestion_UnknownQuestion_ReturnsFallback()
    {
        var advisor = new NexoraAIAdvisor();
        var result = advisor.AnswerQuestion("What is the meaning of life?", MakeMetrics(), MakeHardware());
        Assert.NotEmpty(result);
    }
}
