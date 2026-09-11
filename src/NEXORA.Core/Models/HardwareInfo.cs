using System.Collections.Generic;

namespace NEXORA.Core.Models;

/// <summary>
/// Complete hardware profile detected from the current system.
/// </summary>
public sealed class HardwareInfo
{
    // ── CPU ──────────────────────────────────────────────────────────────
    public string CpuName { get; set; } = string.Empty;
    public int CpuPhysicalCores { get; set; }
    public int CpuLogicalProcessors { get; set; }
    public double CpuBaseClockGHz { get; set; }
    public double CpuMaxBoostGHz { get; set; }
    public string CpuArchitecture { get; set; } = string.Empty;

    // ── GPU ──────────────────────────────────────────────────────────────
    public List<GpuInfo> Gpus { get; set; } = new();
    public GpuInfo? PrimaryGpu => Gpus.Count > 0 ? Gpus[0] : null;

    // ── RAM ──────────────────────────────────────────────────────────────
    public long TotalRamBytes { get; set; }
    public long AvailableRamBytes { get; set; }
    public double TotalRamGB => TotalRamBytes / 1_073_741_824.0;
    public double UsedRamGB => (TotalRamBytes - AvailableRamBytes) / 1_073_741_824.0;
    public double RamUsagePercent => TotalRamBytes > 0
        ? (double)(TotalRamBytes - AvailableRamBytes) / TotalRamBytes * 100.0
        : 0;
    public int RamSpeedMHz { get; set; }
    public string RamType { get; set; } = string.Empty; // DDR4, DDR5, etc.

    // ── Storage ──────────────────────────────────────────────────────────
    public List<StorageDriveInfo> StorageDrives { get; set; } = new();

    // ── Motherboard / System ─────────────────────────────────────────────
    public string MotherboardManufacturer { get; set; } = string.Empty;
    public string MotherboardModel { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;
    public string BiosDate { get; set; } = string.Empty;
    public string SystemManufacturer { get; set; } = string.Empty;
    public string SystemModel { get; set; } = string.Empty;
    public bool IsLaptop { get; set; }

    // ── OS ───────────────────────────────────────────────────────────────
    public string WindowsVersion { get; set; } = string.Empty;
    public string WindowsBuild { get; set; } = string.Empty;
    public string WindowsEdition { get; set; } = string.Empty;
    public string DirectXVersion { get; set; } = string.Empty;

    // ── Display ──────────────────────────────────────────────────────────
    public List<DisplayInfo> Displays { get; set; } = new();
    public DisplayInfo? PrimaryDisplay => Displays.Count > 0 ? Displays[0] : null;

    // ── Power ─────────────────────────────────────────────────────────────
    public string ActivePowerPlan { get; set; } = string.Empty;
    public bool IsOnBattery { get; set; }
    public int BatteryPercent { get; set; } = -1; // -1 = not applicable (desktop)

    // ── Scan metadata ─────────────────────────────────────────────────────
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
}

public sealed class GpuInfo
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty; // NVIDIA, AMD, Intel
    public bool IsDedicated { get; set; }
    public long VramBytes { get; set; }
    public double VramGB => VramBytes / 1_073_741_824.0;
    public string DriverVersion { get; set; } = string.Empty;
    public string DriverDate { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}

public sealed class StorageDriveInfo
{
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public StorageType Type { get; set; } // NVMe, SSD, HDD
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public double TotalGB => TotalBytes / 1_073_741_824.0;
    public double FreeGB => FreeBytes / 1_073_741_824.0;
    public double UsedPercent => TotalBytes > 0
        ? (double)(TotalBytes - FreeBytes) / TotalBytes * 100.0
        : 0;
    public string DriveLetter { get; set; } = string.Empty;
    public string InterfaceType { get; set; } = string.Empty; // NVMe, SATA, USB
}

public enum StorageType { Unknown, HDD, SSD, NVMe }

public sealed class DisplayInfo
{
    public string Name { get; set; } = string.Empty;
    public int ResolutionWidth { get; set; }
    public int ResolutionHeight { get; set; }
    public int RefreshRateHz { get; set; }
    public bool IsPrimary { get; set; }
    public string Resolution => $"{ResolutionWidth}×{ResolutionHeight}";
}
