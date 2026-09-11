using NEXORA.Core.Models;

namespace NEXORA.Core.AI;

/// <summary>
/// NEXORA AI Performance Advisor.
/// Analyzes actual system metrics and provides plain-language explanations
/// and actionable recommendations.
/// All insights are derived from real detected data — never fabricated.
/// </summary>
public sealed class NexoraAIAdvisor
{
    /// <summary>
    /// Analyze live metrics and generate contextual advice.
    /// </summary>
    public AIAnalysisResult AnalyzePerformance(LiveMetrics metrics, HardwareInfo hardware)
    {
        var result = new AIAnalysisResult();
        result.BottleneckType = DetectBottleneck(metrics, hardware);
        result.PrimaryInsight = BuildPrimaryInsight(metrics, result.BottleneckType);
        result.Recommendations = BuildRecommendations(metrics, hardware, result.BottleneckType);
        result.AnalyzedAt = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Answer a natural-language question using actual system data.
    /// </summary>
    public string AnswerQuestion(string question, LiveMetrics metrics, HardwareInfo hardware)
    {
        var q = question.ToLowerInvariant();

        if (q.Contains("fps") && (q.Contains("low") || q.Contains("bad") || q.Contains("why")))
            return ExplainLowFps(metrics, hardware);

        if (q.Contains("stutter") || q.Contains("frame time"))
            return ExplainStutter(metrics, hardware);

        if (q.Contains("temperature") || q.Contains("temp") || q.Contains("hot"))
            return ExplainTemperatures(metrics);

        if (q.Contains("ram") || q.Contains("memory"))
            return ExplainMemory(metrics, hardware);

        if (q.Contains("gpu"))
            return ExplainGpu(metrics, hardware);

        if (q.Contains("cpu"))
            return ExplainCpu(metrics, hardware);

        if (q.Contains("bottleneck"))
            return ExplainBottleneck(metrics, hardware);

        if (q.Contains("optimize") || q.Contains("improve"))
            return SuggestOptimizations(metrics, hardware);

        return "I can analyze your CPU, GPU, RAM, temperatures, FPS, and frame times. "
             + "Try asking: \"Why is my FPS low?\" or \"What is bottlenecking my system?\"";
    }

    // ── Bottleneck detection ──────────────────────────────────────────────────

    private static BottleneckType DetectBottleneck(LiveMetrics m, HardwareInfo hw)
    {
        // Thermal throttling takes priority
        if (m.CpuTempCelsius > 90 || m.GpuTempCelsius > 87)
            return BottleneckType.ThermalThrottling;

        // Both maxed — likely storage or driver issue
        if (m.CpuUsagePercent > 90 && m.GpuUsagePercent > 90)
            return BottleneckType.Balanced;

        // GPU is clearly the bottleneck
        if (m.GpuUsagePercent > 90 && m.CpuUsagePercent < 70)
            return BottleneckType.GpuLimited;

        // CPU is clearly the bottleneck
        if (m.CpuUsagePercent > 90 && m.GpuUsagePercent < 70)
            return BottleneckType.CpuLimited;

        // VRAM near full
        if (hw.PrimaryGpu != null && hw.PrimaryGpu.VramBytes > 0)
        {
            double vramPct = (double)m.VramUsedBytes / hw.PrimaryGpu.VramBytes * 100;
            if (vramPct > 90 && m.GpuUsagePercent < 80)
                return BottleneckType.VramLimited;
        }

        // RAM near full
        if (m.RamUsagePercent > 90)
            return BottleneckType.MemoryLimited;

        // Disk heavily active
        if (m.DiskActivityPercent > 80)
            return BottleneckType.StorageLimited;

        return BottleneckType.Balanced;
    }

    private static string BuildPrimaryInsight(LiveMetrics m, BottleneckType type)
    {
        return type switch
        {
            BottleneckType.GpuLimited =>
                $"Your GPU is running at {m.GpuUsagePercent:F0}% utilization while your CPU is at "
              + $"{m.CpuUsagePercent:F0}%. Your system is GPU-limited. Lowering graphics quality "
              + "settings (shadows, reflections, anti-aliasing) will provide the largest FPS improvement.",

            BottleneckType.CpuLimited =>
                $"Your CPU is running at {m.CpuUsagePercent:F0}% utilization while your GPU is at "
              + $"{m.GpuUsagePercent:F0}%. Your system is CPU-limited. Closing background applications "
              + "and disabling CPU-heavy overlays will help.",

            BottleneckType.VramLimited =>
                $"Your GPU's VRAM is nearly full ({m.VramUsedBytes / 1_048_576.0:F0} MB used). "
              + "Reducing texture quality and shadow resolution will prevent VRAM overflow, "
              + "which can cause severe stuttering.",

            BottleneckType.MemoryLimited =>
                $"Your system RAM usage is at {m.RamUsagePercent:F0}%. Windows is likely using the "
              + "page file, causing storage-related slowdowns. Closing unused applications will help.",

            BottleneckType.StorageLimited =>
                $"Your storage drive is at {m.DiskActivityPercent:F0}% activity. This may cause "
              + "stuttering during asset streaming. An SSD upgrade would provide the largest improvement.",

            BottleneckType.ThermalThrottling =>
                $"Thermal throttling detected. CPU: {m.CpuTempCelsius:F0}°C, GPU: {m.GpuTempCelsius:F0}°C. "
              + "Your hardware is reducing clock speeds to prevent overheating. "
              + "Cleaning dust from cooling vents and improving airflow are the most effective solutions.",

            BottleneckType.Balanced =>
                $"Your system resources are relatively balanced. CPU: {m.CpuUsagePercent:F0}%, "
              + $"GPU: {m.GpuUsagePercent:F0}%, RAM: {m.RamUsagePercent:F0}%. "
              + "No single bottleneck is dominant at this moment.",

            _ => "Insufficient data to determine the primary performance bottleneck."
        };
    }

    private static List<string> BuildRecommendations(LiveMetrics m, HardwareInfo hw, BottleneckType type)
    {
        var recs = new List<string>();

        switch (type)
        {
            case BottleneckType.GpuLimited:
                recs.Add("Lower shadow quality — shadows are among the most GPU-intensive settings.");
                recs.Add("Reduce anti-aliasing from MSAA to FXAA or TAA.");
                recs.Add("Disable or reduce ambient occlusion.");
                recs.Add("Consider lowering render resolution if available.");
                break;
            case BottleneckType.CpuLimited:
                recs.Add("Close browser tabs and background applications before gaming.");
                recs.Add("Disable in-game overlays you are not actively using.");
                recs.Add("Set the game process priority to High in Task Manager.");
                recs.Add("Reduce simulation-heavy settings: NPC count, physics quality.");
                break;
            case BottleneckType.VramLimited:
                recs.Add("Reduce texture quality to Medium or Low.");
                recs.Add("Disable texture streaming if the option exists.");
                recs.Add("Close other GPU-using applications (browsers with hardware acceleration).");
                break;
            case BottleneckType.MemoryLimited:
                recs.Add("Close unused applications and browser tabs.");
                recs.Add($"Your system has {hw.TotalRamGB:F0} GB RAM. 16 GB is recommended for modern games.");
                recs.Add("Disable startup applications that load unnecessarily.");
                break;
            case BottleneckType.ThermalThrottling:
                recs.Add("Clean cooling vents and heatsinks of dust buildup.");
                recs.Add("Ensure the system has adequate airflow.");
                recs.Add("Consider replacing thermal paste if the system is over 3 years old.");
                recs.Add("Use the Balanced or Power Saver profile until temperatures are resolved.");
                break;
        }

        // Universal recommendations
        if (m.CurrentFps > 0 && hw.PrimaryDisplay != null && m.CurrentFps > hw.PrimaryDisplay.RefreshRateHz)
            recs.Add($"Your FPS ({m.CurrentFps:F0}) exceeds your monitor's refresh rate ({hw.PrimaryDisplay.RefreshRateHz} Hz). Enabling VSync or FPS cap will reduce GPU load.");

        return recs;
    }

    // ── Answer builders ───────────────────────────────────────────────────────

    private static string ExplainLowFps(LiveMetrics m, HardwareInfo hw)
    {
        var bottleneck = DetectBottleneck(m, hw);
        return BuildPrimaryInsight(m, bottleneck);
    }

    private static string ExplainStutter(LiveMetrics m, HardwareInfo hw)
    {
        if (m.FrameTimeMs > 33.3)
            return $"Your current frame time is {m.FrameTimeMs:F1} ms, which means you are dropping below 30 FPS. "
                 + "This typically indicates a CPU or GPU bottleneck rather than a raw performance issue.";
        if (m.OnePercentLowFps > 0 && m.AverageFps / m.OnePercentLowFps > 2)
            return $"Your 1% low FPS ({m.OnePercentLowFps:F0}) is significantly lower than your average ({m.AverageFps:F0}). "
                 + "This suggests periodic spikes from background processes or VRAM pressure.";
        if (m.DiskActivityPercent > 70)
            return $"Your storage drive is at {m.DiskActivityPercent:F0}% activity during gaming. "
                 + "Stuttering caused by asset streaming is common when the game is on an HDD.";
        return "Frame pacing looks reasonable based on current data. Stuttering may be caused by driver issues, "
             + "background processes waking up, or thermal throttling events.";
    }

    private static string ExplainTemperatures(LiveMetrics m)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"CPU temperature: {m.CpuTempCelsius:F0}°C");
        sb.AppendLine($"GPU temperature: {m.GpuTempCelsius:F0}°C");

        if (m.CpuTempCelsius > 90)
            sb.AppendLine("⚠ CPU temperature is critically high. Thermal throttling may be occurring.");
        else if (m.CpuTempCelsius > 80)
            sb.AppendLine("CPU temperature is elevated. Ensure cooling is adequate.");
        else
            sb.AppendLine("CPU temperature is within a normal range.");

        if (m.GpuTempCelsius > 87)
            sb.AppendLine("⚠ GPU temperature is critically high.");
        else if (m.GpuTempCelsius > 80)
            sb.AppendLine("GPU temperature is elevated but typically within manufacturer limits.");
        else
            sb.AppendLine("GPU temperature is within a normal range.");

        return sb.ToString().Trim();
    }

    private static string ExplainMemory(LiveMetrics m, HardwareInfo hw)
    {
        return $"RAM usage: {m.RamUsedBytes / 1_073_741_824.0:F1} GB of {hw.TotalRamGB:F0} GB "
             + $"({m.RamUsagePercent:F0}% used). "
             + (m.RamUsagePercent > 85
                ? "Your RAM usage is very high. Windows may be using the page file, "
                  + "which significantly slows down memory-intensive operations."
                : "RAM usage is within a reasonable range.");
    }

    private static string ExplainGpu(LiveMetrics m, HardwareInfo hw)
    {
        var gpu = hw.PrimaryGpu;
        return $"GPU: {gpu?.Name ?? "Unknown"} — currently at {m.GpuUsagePercent:F0}% utilization, "
             + $"{m.GpuTempCelsius:F0}°C. "
             + $"VRAM used: {m.VramUsedBytes / 1_048_576.0:F0} MB of {gpu?.VramGB * 1024:F0} MB.";
    }

    private static string ExplainCpu(LiveMetrics m, HardwareInfo hw)
    {
        return $"CPU: {hw.CpuName} — currently at {m.CpuUsagePercent:F0}% utilization, "
             + $"{m.CpuTempCelsius:F0}°C. "
             + $"Running at approximately {m.CpuClockMHz / 1000.0:F2} GHz.";
    }

    private static string ExplainBottleneck(LiveMetrics m, HardwareInfo hw)
    {
        var type = DetectBottleneck(m, hw);
        return BuildPrimaryInsight(m, type);
    }

    private static string SuggestOptimizations(LiveMetrics m, HardwareInfo hw)
    {
        var type = DetectBottleneck(m, hw);
        var recs = BuildRecommendations(m, hw, type);
        return BuildPrimaryInsight(m, type) + "\n\nRecommended actions:\n"
             + string.Join("\n", recs.Select((r, i) => $"{i + 1}. {r}"));
    }
}

public sealed class AIAnalysisResult
{
    public BottleneckType BottleneckType { get; set; }
    public string PrimaryInsight { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = new();
    public DateTime AnalyzedAt { get; set; }
}
