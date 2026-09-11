using SystemDiag = System.Diagnostics;

namespace NEXORA.Core.Models;

/// <summary>Live performance metrics sampled continuously.</summary>
public sealed class LiveMetrics
{
    public DateTime SampledAt { get; set; } = DateTime.UtcNow;

    // CPU
    public double CpuUsagePercent { get; set; }
    public double CpuTempCelsius { get; set; }
    public double CpuClockMHz { get; set; }

    // GPU
    public double GpuUsagePercent { get; set; }
    public double GpuTempCelsius { get; set; }
    public double GpuClockMHz { get; set; }
    public double VramUsagePercent { get; set; }
    public long VramUsedBytes { get; set; }

    // RAM
    public double RamUsagePercent { get; set; }
    public long RamUsedBytes { get; set; }
    public long RamAvailableBytes { get; set; }

    // Disk
    public double DiskReadMBps { get; set; }
    public double DiskWriteMBps { get; set; }
    public double DiskActivityPercent { get; set; }

    // Network
    public double NetworkDownloadMBps { get; set; }
    public double NetworkUploadMBps { get; set; }

    // FPS (only populated when a frame-time source is active)
    public double CurrentFps { get; set; }
    public double AverageFps { get; set; }
    public double OnePercentLowFps { get; set; }
    public double FrameTimeMs { get; set; }

    // Power
    public bool IsOnBattery { get; set; }
    public int BatteryPercent { get; set; } = -1;
}

/// <summary>Startup application entry.</summary>
public sealed class StartupEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string RegistryKey { get; set; } = string.Empty;
    public StartupLocation Location { get; set; }
    public StartupImpact Impact { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsCriticalSystem { get; set; }
    public string Description { get; set; } = string.Empty;
}

public enum StartupLocation { HKCU_Run, HKLM_Run, StartupFolder, TaskScheduler }
public enum StartupImpact { None, Low, Medium, High, Critical }

/// <summary>Running process info.</summary>
public sealed class ProcessEntry
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public double CpuUsagePercent { get; set; }
    public long MemoryBytes { get; set; }
    public double MemoryMB => MemoryBytes / 1_048_576.0;
    public double GpuUsagePercent { get; set; }
    public long VramBytes { get; set; }
    public double DiskMBps { get; set; }
    public double NetworkMBps { get; set; }
    public SystemDiag.ProcessPriorityClass Priority { get; set; }
    public bool IsCriticalSystem { get; set; }
}

/// <summary>Storage cleanup category.</summary>
public sealed class CleanupCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long ReclaimableBytes { get; set; }
    public double ReclaimableGB => ReclaimableBytes / 1_073_741_824.0;
    public double ReclaimableMB => ReclaimableBytes / 1_048_576.0;
    public List<string> FilePaths { get; set; } = new();
    public bool IsSelected { get; set; } = true;
    public bool IsSafe { get; set; } = true;
    public RiskLevel Risk { get; set; } = RiskLevel.Safe;
}

/// <summary>Network diagnostic result.</summary>
public sealed class NetworkDiagnostics
{
    public double PingMs { get; set; }
    public double JitterMs { get; set; }
    public double PacketLossPercent { get; set; }
    public double DnsResponseMs { get; set; }
    public string AdapterName { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty; // Ethernet, Wi-Fi
    public string IpAddress { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = new();
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>NEXORA Performance Score breakdown.</summary>
public sealed class PerformanceScore
{
    public int Overall { get; set; }
    public int CpuScore { get; set; }
    public int GpuScore { get; set; }
    public int MemoryScore { get; set; }
    public int StorageScore { get; set; }
    public int GamingReadiness { get; set; }
    public List<string> Explanations { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
