// Mock / fallback data layer.
// Pages use real service data first; mock data fills gaps where the service
// does not yet provide a value (e.g. FPS, game library, benchmark scores).
// No emojis — UI draws its own icons.

namespace NEXORA.Design;

// ── Hardware fallback (shown until real scan completes) ───────────────────────

public sealed class MockHardwareData
{
    public string CpuName        { get; set; } = "Scanning…";
    public int    CpuCores       { get; set; } = 0;
    public double CpuUsage       { get; set; } = 0;
    public double CpuTemp        { get; set; } = 0;
    public double CpuClockGHz    { get; set; } = 0;

    public string GpuName        { get; set; } = "Scanning…";
    public double GpuUsage       { get; set; } = 0;
    public double GpuTemp        { get; set; } = 0;
    public double GpuVramGB      { get; set; } = 0;
    public double GpuVramUsed    { get; set; } = 0;

    public double RamTotalGB     { get; set; } = 0;
    public double RamUsedGB      { get; set; } = 0;
    public double RamUsagePct    { get; set; } = 0;
    public string RamType        { get; set; } = "—";

    public string StorageName    { get; set; } = "—";
    public double StorageTotalGB { get; set; } = 0;
    public double StorageFreeGB  { get; set; } = 0;
    public double StorageUsagePct{ get; set; } = 0;
    public double DiskActivity   { get; set; } = 0;

    public string WindowsVersion { get; set; } = "—";
    public string DirectX        { get; set; } = "—";
    public string DisplayRes     { get; set; } = "—";
    public string PowerPlan      { get; set; } = "—";

    public int NexoraScore       { get; set; } = 0;
    public int CpuScore          { get; set; } = 0;
    public int GpuScore          { get; set; } = 0;
    public int MemScore          { get; set; } = 0;
    public int StorageScore      { get; set; } = 0;
    public int GamingScore       { get; set; } = 0;

    public static MockHardwareData Instance { get; } = new();

    /// <summary>Populate from real scanned hardware.</summary>
    public static MockHardwareData FromRealHardware(
        NEXORA.Core.Models.HardwareInfo hw,
        NEXORA.Core.Models.PerformanceScore score,
        NEXORA.Core.Models.LiveMetrics? live = null)
    {
        var d = Instance;
        d.CpuName     = hw.CpuName;
        d.CpuCores    = hw.CpuLogicalProcessors;
        d.CpuClockGHz = hw.CpuBaseClockGHz;
        d.CpuUsage    = live?.CpuUsagePercent ?? 0;
        d.CpuTemp     = live?.CpuTempCelsius  ?? 0;

        if (hw.PrimaryGpu != null)
        {
            d.GpuName    = hw.PrimaryGpu.Name;
            d.GpuVramGB  = hw.PrimaryGpu.VramGB;
        }
        d.GpuUsage   = live?.GpuUsagePercent ?? 0;
        d.GpuTemp    = live?.GpuTempCelsius  ?? 0;
        d.GpuVramUsed = live != null && hw.PrimaryGpu?.VramBytes > 0
            ? live.VramUsedBytes / 1_073_741_824.0 : 0;

        d.RamTotalGB  = hw.TotalRamGB;
        d.RamUsedGB   = live != null ? live.RamUsedBytes / 1_073_741_824.0 : hw.UsedRamGB;
        d.RamUsagePct = live?.RamUsagePercent ?? hw.RamUsagePercent;
        d.RamType     = $"{hw.RamType} {hw.RamSpeedMHz} MHz".Trim();

        var sys = hw.StorageDrives.FirstOrDefault(dr => dr.DriveLetter.StartsWith("C"))
                  ?? hw.StorageDrives.FirstOrDefault();
        if (sys != null)
        {
            d.StorageName     = sys.Type.ToString();
            d.StorageTotalGB  = sys.TotalGB;
            d.StorageFreeGB   = sys.FreeGB;
            d.StorageUsagePct = sys.UsedPercent;
        }
        d.DiskActivity = live?.DiskActivityPercent ?? 0;

        d.WindowsVersion = $"{hw.WindowsVersion} ({hw.WindowsBuild})";
        d.DirectX        = hw.DirectXVersion;
        d.DisplayRes     = hw.PrimaryDisplay != null
            ? $"{hw.PrimaryDisplay.Resolution} @ {hw.PrimaryDisplay.RefreshRateHz} Hz"
            : "—";
        d.PowerPlan = hw.ActivePowerPlan;

        d.NexoraScore  = score.Overall;
        d.CpuScore     = score.CpuScore;
        d.GpuScore     = score.GpuScore;
        d.MemScore     = score.MemoryScore;
        d.StorageScore = score.StorageScore;
        d.GamingScore  = score.GamingReadiness;

        return d;
    }
}

// ── Performance graph data — seeded as flat lines, updated with real metrics ──

public sealed class MockPerformanceData
{
    private static readonly Random _rng = new(Environment.TickCount);

    public static List<double> CpuHistory    { get; } = Flat(0, 60);
    public static List<double> GpuHistory    { get; } = Flat(0, 60);
    public static List<double> RamHistory    { get; } = Flat(0, 60);
    public static List<double> FpsHistory    { get; } = Flat(0, 60);
    public static List<double> NetDownHist   { get; } = Flat(0, 60);
    public static List<double> NetUpHist     { get; } = Flat(0, 60);

    // Live values — set by DashboardViewModel real monitoring
    public static double CurrentCpu      { get; set; } = 0;
    public static double CurrentGpu      { get; set; } = 0;
    public static double CurrentRam      { get; set; } = 0;
    public static double CurrentFps      { get; set; } = 0;
    public static double OnePctLow       { get; set; } = 0;
    public static double FrameTimeMs     { get; set; } = 0;

    // Network — from LiveMetrics (LHWM throughput sensors)
    public static double PingMs          { get; set; } = 0;
    public static double JitterMs        { get; set; } = 0;
    public static double PacketLoss      { get; set; } = 0;
    public static double DownloadMbps    { get; set; } = 0;
    public static double UploadMbps      { get; set; } = 0;

    /// <summary>Push a new real sample into the history ring buffer.</summary>
    public static void PushSample(double cpu, double gpu, double ram)
    {
        Push(CpuHistory, cpu);
        Push(GpuHistory, gpu);
        Push(RamHistory, ram);
        CurrentCpu = cpu; CurrentGpu = gpu; CurrentRam = ram;
    }

    private static void Push(List<double> list, double value)
    {
        if (list.Count >= 60) list.RemoveAt(0);
        list.Add(Math.Clamp(Math.Round(value, 1), 0, 100));
    }

    private static List<double> Flat(double val, int count)
        => Enumerable.Repeat(val, count).ToList();

    public static List<double> GenerateSeries(double center, double noise, int count)
    {
        var list = new List<double>(count);
        var v = center;
        for (int i = 0; i < count; i++)
        {
            v += (_rng.NextDouble() - 0.5) * noise;
            v  = Math.Clamp(v, 1, 100);
            list.Add(Math.Round(v, 1));
        }
        return list;
    }
}

// ── Games — shown in Game Library / Optimizer until real detection completes ──

public sealed class MockGame
{
    public string Name       { get; set; } = string.Empty;
    public string Profile    { get; set; } = "Balanced";
    public int    TargetFps  { get; set; } = 60;
    public int    CurrentFps { get; set; } = 60;
    public string Status     { get; set; } = "Not Optimized";
    public string Store      { get; set; } = "Steam";
    public int    CpuImpact  { get; set; } = 60;
    public int    GpuImpact  { get; set; } = 85;
    public int    RamImpact  { get; set; } = 45;
}

public static class MockGameData
{
    public static readonly List<MockGame> Games = new()
    {
        new() { Name="VALORANT",         Profile="Competitive", TargetFps=144, CurrentFps=118, Status="Optimizable", Store="Riot",     CpuImpact=55, GpuImpact=70, RamImpact=40 },
        new() { Name="GTA V",            Profile="Balanced",    TargetFps=75,  CurrentFps=62,  Status="Optimized",   Store="Rockstar", CpuImpact=75, GpuImpact=90, RamImpact=65 },
        new() { Name="FiveM",            Profile="Performance", TargetFps=60,  CurrentFps=54,  Status="Optimizable", Store="Custom",   CpuImpact=80, GpuImpact=75, RamImpact=55 },
        new() { Name="Fortnite",         Profile="Competitive", TargetFps=120, CurrentFps=98,  Status="Optimizable", Store="Epic",     CpuImpact=60, GpuImpact=80, RamImpact=50 },
        new() { Name="Minecraft",        Profile="Balanced",    TargetFps=60,  CurrentFps=58,  Status="Optimized",   Store="Microsoft",CpuImpact=70, GpuImpact=40, RamImpact=70 },
        new() { Name="CS2",              Profile="Competitive", TargetFps=250, CurrentFps=180, Status="Optimizable", Store="Steam",    CpuImpact=65, GpuImpact=85, RamImpact=40 },
        new() { Name="Apex Legends",     Profile="Competitive", TargetFps=144, CurrentFps=110, Status="Optimizable", Store="EA",       CpuImpact=70, GpuImpact=88, RamImpact=55 },
        new() { Name="League of Legends",Profile="Maximum",     TargetFps=165, CurrentFps=160, Status="Optimized",   Store="Riot",     CpuImpact=45, GpuImpact=50, RamImpact=35 },
    };
}

// ── Benchmark — placeholder until real benchmark engine is implemented ────────

public static class MockBenchmarkData
{
    public static int CpuScore      { get; } = 0;
    public static int GpuScore      { get; } = 0;
    public static int MemoryScore   { get; } = 0;
    public static int GamingScore   { get; } = 0;
    public static int OverallScore  { get; } = 0;
    public static int PreviousScore { get; } = 0;
    public static double Improvement => 0;
    public static List<(string Label, int Score)> History { get; } = new();
}

// ── Storage — colour per category (no emoji) ─────────────────────────────────

public sealed class MockStorageItem
{
    public string Name   { get; set; } = string.Empty;
    public double SizeGB { get; set; }
    public Windows.UI.Color Color { get; set; }
}

public static class MockStorageData
{
    // Populated by real scan; pre-filled with zeros so UI renders cleanly
    public static List<MockStorageItem> Items { get; set; } = new()
    {
        new() { Name="Temp Files",     SizeGB=0, Color=Windows.UI.Color.FromArgb(0xFF,0x00,0xC8,0xFF) },
        new() { Name="Browser Cache",  SizeGB=0, Color=Windows.UI.Color.FromArgb(0xFF,0x7B,0x2F,0xFF) },
        new() { Name="App Logs",       SizeGB=0, Color=Windows.UI.Color.FromArgb(0xFF,0xFF,0xC1,0x07) },
        new() { Name="Recycle Bin",    SizeGB=0, Color=Windows.UI.Color.FromArgb(0xFF,0xFF,0x44,0x44) },
        new() { Name="Crash Dumps",    SizeGB=0, Color=Windows.UI.Color.FromArgb(0xFF,0x00,0xE6,0x76) },
    };
    public static double TotalGB => Items.Sum(i => i.SizeGB);
}

// ── Restore — shown until real sessions load ──────────────────────────────────

public sealed class MockRestoreEntry
{
    public string Name      { get; set; } = string.Empty;
    public string When      { get; set; } = string.Empty;
    public int    Changes   { get; set; }
    public bool   Restorable{ get; set; } = true;
}

public static class MockRestoreData
{
    // Empty — only show real sessions
    public static readonly List<MockRestoreEntry> Entries = new();
}

// ── Startup — no mock; always show real registry entries ─────────────────────

public sealed class MockStartupEntry
{
    public string Name      { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Impact    { get; set; } = "Low";
    public bool   Enabled   { get; set; } = true;
    public string StartTime { get; set; } = "—";
    public Windows.UI.Color ImpactColor => Impact switch
    {
        "High"     => Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x44, 0x44),
        "Medium"   => Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xC1, 0x07),
        "Critical" => Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x00, 0x55),
        _          => Windows.UI.Color.FromArgb(0xFF, 0x00, 0xE6, 0x76)
    };
}

public static class MockStartupData
{
    // Empty — StartupManagerPage loads real entries from StartupManagerService
    public static readonly List<MockStartupEntry> Entries = new();
}
