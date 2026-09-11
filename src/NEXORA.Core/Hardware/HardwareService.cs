using System.Management;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;

namespace NEXORA.Core.Hardware;

/// <summary>
/// Uses WMI + LibreHardwareMonitor to detect system hardware and
/// sample live performance metrics.
/// </summary>
public sealed class HardwareService : IHardwareService, IDisposable
{
    private readonly ILogger<HardwareService> _logger;
    private HardwareInfo? _cached;
    private readonly Computer _computer;
    private readonly object _lhwmLock = new();

    public HardwareService(ILogger<HardwareService> logger)
    {
        _logger = logger;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true,
            IsNetworkEnabled = true
        };
        try { _computer.Open(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LibreHardwareMonitor could not be opened (may need admin rights for full sensor data).");
        }
    }

    public HardwareInfo? GetCachedInfo() => _cached;

    public async Task<HardwareInfo> ScanAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var info = new HardwareInfo();

        progress?.Report("Scanning CPU…");
        await ScanCpuAsync(info, ct);

        progress?.Report("Scanning GPU…");
        await ScanGpuAsync(info, ct);

        progress?.Report("Scanning RAM…");
        await ScanRamAsync(info, ct);

        progress?.Report("Scanning storage…");
        await ScanStorageAsync(info, ct);

        progress?.Report("Reading motherboard info…");
        await ScanMotherboardAsync(info, ct);

        progress?.Report("Reading OS information…");
        await ScanOsAsync(info, ct);

        progress?.Report("Reading display info…");
        await ScanDisplaysAsync(info, ct);

        progress?.Report("Reading power plan…");
        await ScanPowerAsync(info, ct);

        progress?.Report("Detecting form factor…");
        info.IsLaptop = DetectLaptop();

        info.ScannedAt = DateTime.UtcNow;
        _cached = info;

        progress?.Report("Hardware scan complete.");
        return info;
    }

    // ── CPU ──────────────────────────────────────────────────────────────────

    private async Task ScanCpuAsync(HardwareInfo info, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.CpuName = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                    info.CpuPhysicalCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                    info.CpuLogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                    info.CpuBaseClockGHz = Convert.ToDouble(obj["MaxClockSpeed"] ?? 0) / 1000.0;
                    info.CpuArchitecture = obj["Architecture"]?.ToString() switch
                    {
                        "9" => "x64",
                        "12" => "ARM64",
                        _ => "x86"
                    };
                    break; // Use first CPU
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "CPU scan failed."); }
        }, ct);
    }

    // ── GPU ──────────────────────────────────────────────────────────────────

    private async Task ScanGpuAsync(HardwareInfo info, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var gpu = new GpuInfo
                    {
                        Name = name,
                        DriverVersion = obj["DriverVersion"]?.ToString() ?? string.Empty,
                        DriverDate = ParseDriverDate(obj["DriverDate"]?.ToString()),
                        DeviceId = obj["PNPDeviceID"]?.ToString() ?? string.Empty
                    };

                    // Determine manufacturer
                    var nameLower = name.ToLowerInvariant();
                    if (nameLower.Contains("nvidia")) gpu.Manufacturer = "NVIDIA";
                    else if (nameLower.Contains("amd") || nameLower.Contains("radeon")) gpu.Manufacturer = "AMD";
                    else if (nameLower.Contains("intel")) gpu.Manufacturer = "Intel";
                    else gpu.Manufacturer = "Unknown";

                    // Dedicated = has its own VRAM reported
                    var vramRaw = Convert.ToInt64(obj["AdapterRAM"] ?? 0L);
                    gpu.VramBytes = vramRaw > 0 ? vramRaw : 0;
                    gpu.IsDedicated = vramRaw >= 1_073_741_824; // ≥ 1 GB

                    info.Gpus.Add(gpu);
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "GPU scan failed."); }
        }, ct);
    }

    // ── RAM ──────────────────────────────────────────────────────────────────

    private async Task ScanRamAsync(HardwareInfo info, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                // Total/available via OS
                using var os = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in os.Get())
                {
                    var totalKb = Convert.ToInt64(obj["TotalVisibleMemorySize"] ?? 0L);
                    var freeKb = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0L);
                    info.TotalRamBytes = totalKb * 1024L;
                    info.AvailableRamBytes = freeKb * 1024L;
                    break;
                }

                // Speed + type via physical memory
                using var mem = new ManagementObjectSearcher("SELECT Speed,SMBIOSMemoryType FROM Win32_PhysicalMemory");
                foreach (ManagementObject obj in mem.Get())
                {
                    info.RamSpeedMHz = Convert.ToInt32(obj["Speed"] ?? 0);
                    info.RamType = (Convert.ToInt32(obj["SMBIOSMemoryType"] ?? 0)) switch
                    {
                        26 => "DDR4",
                        34 => "DDR5",
                        24 => "DDR3",
                        _ => "DDR"
                    };
                    break;
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "RAM scan failed."); }
        }, ct);
    }

    // ── Storage ──────────────────────────────────────────────────────────────

    private async Task ScanStorageAsync(HardwareInfo info, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                // Drive models
                var models = new Dictionary<string, string>();
                using var diskSearch = new ManagementObjectSearcher("SELECT Model,SerialNumber,InterfaceType FROM Win32_DiskDrive");
                foreach (ManagementObject obj in diskSearch.Get())
                {
                    var model = obj["Model"]?.ToString() ?? string.Empty;
                    var serial = obj["SerialNumber"]?.ToString()?.Trim() ?? string.Empty;
                    var iface = obj["InterfaceType"]?.ToString() ?? string.Empty;
                    models[model] = iface;
                }

                // Logical drives
                foreach (var drive in System.IO.DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    var sd = new StorageDriveInfo
                    {
                        DriveLetter = drive.Name,
                        TotalBytes = drive.TotalSize,
                        FreeBytes = drive.TotalFreeSpace,
                        Model = drive.VolumeLabel.Length > 0 ? drive.VolumeLabel : drive.Name,
                        InterfaceType = "Unknown"
                    };

                    // Classify type heuristically
                    sd.Type = ClassifyStorageType(sd.Model, sd.InterfaceType);
                    info.StorageDrives.Add(sd);
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Storage scan failed."); }
        }, ct);
    }

    private static StorageType ClassifyStorageType(string model, string iface)
    {
        var m = model.ToLowerInvariant();
        var i = iface.ToLowerInvariant();
        if (m.Contains("nvme") || i.Contains("nvme")) return StorageType.NVMe;
        if (m.Contains("ssd") || m.Contains("solid")) return StorageType.SSD;
        if (i == "scsi" || m.Contains("hdd") || m.Contains("wd") || m.Contains("seagate")) return StorageType.HDD;
        return StorageType.Unknown;
    }

    // ── Motherboard ──────────────────────────────────────────────────────────

    private async Task ScanMotherboardAsync(HardwareInfo info, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                using var mb = new ManagementObjectSearcher("SELECT Manufacturer,Product FROM Win32_BaseBoard");
                foreach (ManagementObject obj in mb.Get())
                {
                    info.MotherboardManufacturer = obj["Manufacturer"]?.ToString() ?? string.Empty;
                    info.MotherboardModel = obj["Product"]?.ToString() ?? string.Empty;
                    break;
                }

                using var bios = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion,ReleaseDate FROM Win32_BIOS");
                foreach (ManagementObject obj in bios.Get())
                {
                    info.BiosVersion = obj["SMBIOSBIOSVersion"]?.ToString() ?? string.Empty;
                    info.BiosDate = ParseDriverDate(obj["ReleaseDate"]?.ToString());
                    break;
                }

                using var sys = new ManagementObjectSearcher("SELECT Manufacturer,Model FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in sys.Get())
                {
                    info.SystemManufacturer = obj["Manufacturer"]?.ToString() ?? string.Empty;
                    info.SystemModel = obj["Model"]?.ToString() ?? string.Empty;
                    break;
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Motherboard scan failed."); }
        }, ct);
    }

    // ── OS ───────────────────────────────────────────────────────────────────

    private async Task ScanOsAsync(HardwareInfo info, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                using var os = new ManagementObjectSearcher("SELECT Caption,Version,BuildNumber FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in os.Get())
                {
                    info.WindowsEdition = obj["Caption"]?.ToString() ?? string.Empty;
                    info.WindowsVersion = ParseWindowsVersion(info.WindowsEdition);
                    info.WindowsBuild = obj["BuildNumber"]?.ToString() ?? string.Empty;
                    break;
                }

                // DirectX version approximation from OS build
                if (int.TryParse(info.WindowsBuild, out var build))
                    info.DirectXVersion = build >= 22000 ? "DirectX 12 Ultimate" : "DirectX 12";
            }
            catch (Exception ex) { _logger.LogError(ex, "OS scan failed."); }
        }, ct);
    }

    private static string ParseWindowsVersion(string caption)
    {
        if (caption.Contains("11")) return "Windows 11";
        if (caption.Contains("10")) return "Windows 10";
        return caption;
    }

    // ── Displays ─────────────────────────────────────────────────────────────

    private async Task ScanDisplaysAsync(HardwareInfo info, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                bool first = true;
                foreach (ManagementObject obj in searcher.Get())
                {
                    var w = Convert.ToInt32(obj["CurrentHorizontalResolution"] ?? 0);
                    var h = Convert.ToInt32(obj["CurrentVerticalResolution"] ?? 0);
                    var r = Convert.ToInt32(obj["CurrentRefreshRate"] ?? 60);
                    if (w == 0) continue;

                    info.Displays.Add(new DisplayInfo
                    {
                        Name = obj["Name"]?.ToString() ?? $"Display {info.Displays.Count + 1}",
                        ResolutionWidth = w,
                        ResolutionHeight = h,
                        RefreshRateHz = r,
                        IsPrimary = first
                    });
                    first = false;
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Display scan failed."); }
        }, ct);
    }

    // ── Power ─────────────────────────────────────────────────────────────────

    private async Task ScanPowerAsync(HardwareInfo info, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                // Active power plan
                var result = RunPowercfg("/getactivescheme");
                if (result.Contains("High performance", StringComparison.OrdinalIgnoreCase))
                    info.ActivePowerPlan = "High Performance";
                else if (result.Contains("Balanced", StringComparison.OrdinalIgnoreCase))
                    info.ActivePowerPlan = "Balanced";
                else if (result.Contains("Power saver", StringComparison.OrdinalIgnoreCase))
                    info.ActivePowerPlan = "Power Saver";
                else
                    info.ActivePowerPlan = result.Trim();

                // Battery
                using var battery = new ManagementObjectSearcher("SELECT BatteryStatus,EstimatedChargeRemaining FROM Win32_Battery");
                foreach (ManagementObject obj in battery.Get())
                {
                    var status = Convert.ToInt32(obj["BatteryStatus"] ?? 0);
                    info.IsOnBattery = status == 1;
                    info.BatteryPercent = Convert.ToInt32(obj["EstimatedChargeRemaining"] ?? -1);
                    break;
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Power scan failed."); }
        }, ct);
    }

    private static bool DetectLaptop()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT PCSystemType FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var type = Convert.ToInt32(obj["PCSystemType"] ?? 0);
                // 2 = Mobile (laptop)
                return type == 2;
            }
        }
        catch { /* ignore */ }
        return false;
    }

    // ── Live Metrics ─────────────────────────────────────────────────────────

    public async Task<LiveMetrics> GetLiveMetricsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var m = new LiveMetrics { SampledAt = DateTime.UtcNow };
            try
            {
                lock (_lhwmLock)
                {
                    foreach (var hw in _computer.Hardware)
                    {
                        hw.Update();
                        ReadHardwareSensors(hw, m);
                        foreach (var sub in hw.SubHardware)
                        {
                            sub.Update();
                            ReadHardwareSensors(sub, m);
                        }
                    }
                }

                // RAM available via OS (more accurate than LHWM for available)
                using var os = new ManagementObjectSearcher("SELECT FreePhysicalMemory,TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in os.Get())
                {
                    var total = Convert.ToInt64(obj["TotalVisibleMemorySize"] ?? 0L) * 1024L;
                    var free = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0L) * 1024L;
                    m.RamAvailableBytes = free;
                    m.RamUsedBytes = total - free;
                    m.RamUsagePercent = total > 0 ? (double)m.RamUsedBytes / total * 100.0 : 0;
                    break;
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Live metrics sampling failed."); }
            return m;
        }, ct);
    }

    private static void ReadHardwareSensors(IHardware hw, LiveMetrics m)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.Value is null) continue;
            var v = sensor.Value.Value;

            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total"))
                        m.CpuUsagePercent = v;
                    if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core"))
                        m.CpuTempCelsius = Math.Max(m.CpuTempCelsius, v);
                    if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core #1"))
                        m.CpuClockMHz = v;
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                        m.GpuUsagePercent = v;
                    if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core"))
                        m.GpuTempCelsius = v;
                    if (sensor.SensorType == SensorType.SmallData && sensor.Name.Contains("GPU Memory Used"))
                        m.VramUsedBytes = (long)(v * 1_048_576);
                    if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Memory"))
                        m.VramUsagePercent = v;
                    break;

                case HardwareType.Storage:
                    if (sensor.SensorType == SensorType.Load)
                        m.DiskActivityPercent = v;
                    if (sensor.SensorType == SensorType.Throughput && sensor.Name.Contains("Read"))
                        m.DiskReadMBps = v;
                    if (sensor.SensorType == SensorType.Throughput && sensor.Name.Contains("Write"))
                        m.DiskWriteMBps = v;
                    break;

                case HardwareType.Network:
                    if (sensor.SensorType == SensorType.Throughput && sensor.Name.Contains("Download"))
                        m.NetworkDownloadMBps = v;
                    if (sensor.SensorType == SensorType.Throughput && sensor.Name.Contains("Upload"))
                        m.NetworkUploadMBps = v;
                    break;
            }
        }
    }

    // ── Score ─────────────────────────────────────────────────────────────────

    public async Task<PerformanceScore> CalculateScoreAsync(HardwareInfo hardware, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var score = new PerformanceScore();
            var explanations = new List<string>();

            // CPU score (0-100)
            score.CpuScore = CalculateCpuScore(hardware, explanations);

            // GPU score (0-100)
            score.GpuScore = CalculateGpuScore(hardware, explanations);

            // Memory score (0-100)
            score.MemoryScore = CalculateMemoryScore(hardware, explanations);

            // Storage score (0-100)
            score.StorageScore = CalculateStorageScore(hardware, explanations);

            // Gaming readiness
            score.GamingReadiness = (score.CpuScore + score.GpuScore * 2 + score.MemoryScore + score.StorageScore) / 5;

            // Overall weighted average
            score.Overall = (score.CpuScore * 2 + score.GpuScore * 3 + score.MemoryScore + score.StorageScore + score.GamingReadiness) / 8;
            score.Overall = Math.Clamp(score.Overall, 0, 100);

            score.Explanations = explanations;
            score.CalculatedAt = DateTime.UtcNow;
            return score;
        }, ct);
    }

    private static int CalculateCpuScore(HardwareInfo hw, List<string> exp)
    {
        int score = 50;
        if (hw.CpuLogicalProcessors >= 16) { score += 25; exp.Add("CPU has 16+ logical processors — excellent multi-threaded capacity."); }
        else if (hw.CpuLogicalProcessors >= 8) { score += 15; }
        else if (hw.CpuLogicalProcessors >= 4) { score += 5; }
        else { score -= 15; exp.Add("CPU has fewer than 4 logical processors — may bottleneck modern games."); }

        if (hw.CpuBaseClockGHz >= 4.0) score += 15;
        else if (hw.CpuBaseClockGHz >= 3.0) score += 8;
        else if (hw.CpuBaseClockGHz < 2.0) { score -= 10; exp.Add("CPU base clock below 2 GHz — may limit single-threaded performance."); }

        return Math.Clamp(score, 0, 100);
    }

    private static int CalculateGpuScore(HardwareInfo hw, List<string> exp)
    {
        if (hw.PrimaryGpu is null) return 20;
        int score = 40;
        var gpu = hw.PrimaryGpu;

        if (gpu.VramGB >= 8) { score += 30; }
        else if (gpu.VramGB >= 4) { score += 15; }
        else if (gpu.VramGB >= 2) { score += 5; }
        else { score -= 10; exp.Add("GPU has less than 2 GB VRAM — will struggle at 1080p with medium+ settings."); }

        if (!gpu.IsDedicated) { score -= 20; exp.Add("Integrated GPU detected — gaming performance will be significantly limited."); }

        return Math.Clamp(score, 0, 100);
    }

    private static int CalculateMemoryScore(HardwareInfo hw, List<string> exp)
    {
        int score = 50;
        if (hw.TotalRamGB >= 32) score += 30;
        else if (hw.TotalRamGB >= 16) score += 20;
        else if (hw.TotalRamGB >= 8) score += 5;
        else { score -= 20; exp.Add($"Only {hw.TotalRamGB:F0} GB RAM — 16 GB is recommended for modern gaming."); }

        if (hw.RamSpeedMHz >= 3600) score += 10;
        else if (hw.RamSpeedMHz >= 3200) score += 5;

        return Math.Clamp(score, 0, 100);
    }

    private static int CalculateStorageScore(HardwareInfo hw, List<string> exp)
    {
        int score = 50;
        bool hasNvme = hw.StorageDrives.Any(d => d.Type == StorageType.NVMe);
        bool hasSsd = hw.StorageDrives.Any(d => d.Type == StorageType.SSD);
        bool allHdd = hw.StorageDrives.All(d => d.Type == StorageType.HDD);

        if (hasNvme) { score += 30; }
        else if (hasSsd) { score += 15; }
        else if (allHdd) { score -= 20; exp.Add("No SSD detected — game load times will be significantly slower."); }

        // Low free space penalty
        var sysDrive = hw.StorageDrives.FirstOrDefault(d => d.DriveLetter.StartsWith("C"));
        if (sysDrive != null && sysDrive.FreeGB < 10)
        { score -= 15; exp.Add($"System drive has only {sysDrive.FreeGB:F1} GB free — performance may be affected."); }

        return Math.Clamp(score, 0, 100);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string RunPowercfg(string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("powercfg", args)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            return proc?.StandardOutput.ReadToEnd() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string ParseDriverDate(string? wmiDate)
    {
        if (string.IsNullOrWhiteSpace(wmiDate) || wmiDate.Length < 8) return string.Empty;
        try
        {
            var year = wmiDate[..4];
            var month = wmiDate[4..6];
            var day = wmiDate[6..8];
            return $"{year}-{month}-{day}";
        }
        catch { return string.Empty; }
    }

    public void Dispose()
    {
        try { _computer.Close(); } catch { /* ignore */ }
    }
}
