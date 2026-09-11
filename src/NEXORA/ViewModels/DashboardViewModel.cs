using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;
using System.Collections.ObjectModel;

namespace NEXORA.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IHardwareService _hardware;
    private readonly IOptimizationEngine _engine;
    private CancellationTokenSource? _monitorCts;
    private PeriodicTimer? _timer;

    // ── Hardware summary ──────────────────────────────────────────────────
    [ObservableProperty] private string _cpuName = "—";
    [ObservableProperty] private string _gpuName = "—";
    [ObservableProperty] private string _ramSummary = "—";
    [ObservableProperty] private string _storageSummary = "—";
    [ObservableProperty] private string _displaySummary = "—";
    [ObservableProperty] private string _windowsVersion = "—";
    [ObservableProperty] private bool _isLaptop;

    // ── Live metrics ──────────────────────────────────────────────────────
    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private double _gpuUsage;
    [ObservableProperty] private double _ramUsage;
    [ObservableProperty] private double _vramUsage;
    [ObservableProperty] private double _diskActivity;
    [ObservableProperty] private double _cpuTemp;
    [ObservableProperty] private double _gpuTemp;
    [ObservableProperty] private double _currentFps;
    [ObservableProperty] private double _frameTimeMs;
    [ObservableProperty] private double _onePercentLow;
    [ObservableProperty] private string _powerPlan = "—";
    [ObservableProperty] private int _batteryPercent = -1;
    [ObservableProperty] private bool _isOnBattery;

    // ── Score ─────────────────────────────────────────────────────────────
    [ObservableProperty] private PerformanceScore? _score;

    // ── Optimizer readiness ───────────────────────────────────────────────
    [ObservableProperty] private int _availableOptimizations;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isOptimizing;
    [ObservableProperty] private string _statusMessage = "Click SCAN to analyse your system.";

    // ── Recent optimizations ──────────────────────────────────────────────
    public ObservableCollection<string> RecentActivities { get; } = new();

    public DashboardViewModel(IHardwareService hardware, IOptimizationEngine engine)
    {
        _hardware = hardware;
        _engine = engine;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning hardware…";
        try
        {
            var hw = await _hardware.ScanAsync(new Progress<string>(m =>
            {
                StatusMessage = m;
                App.Log($"[HW] {m}");
            }));
            PopulateHardwareSummary(hw);

            Score = await _hardware.CalculateScoreAsync(hw);

            // Write real hardware data into the shared MockHardwareData.Instance so all
            // pages (Performance, StorageCleaner, GameOptimizer, Benchmark, Dashboard)
            // display accurate real data rather than placeholder zeros.
            NEXORA.Design.MockHardwareData.FromRealHardware(hw, Score);
            App.Log($"[RealData] CPU={hw.CpuName} GPU={hw.PrimaryGpu?.Name} RAM={hw.TotalRamGB:F1}GB Score={Score.Overall}");

            // Run analysis on a background thread — some modules scan disk/registry
            var opts = await Task.Run(() =>
                _engine.AnalyzeAsync(OptimizationLevel.Balanced, hw), CancellationToken.None);
            AvailableOptimizations = opts.Count;
            StatusMessage = $"{AvailableOptimizations} optimizations available.";
            App.Log($"[Dashboard] Loaded OK — {AvailableOptimizations} opts");

            StartMonitoring();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
            App.Log($"[Dashboard] ERROR: {ex}");
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void PopulateHardwareSummary(HardwareInfo hw)
    {
        CpuName = hw.CpuName;
        GpuName = hw.PrimaryGpu?.Name ?? "Integrated Graphics";
        RamSummary = $"{hw.TotalRamGB:F0} GB {hw.RamType} @ {hw.RamSpeedMHz} MHz";
        StorageSummary = hw.StorageDrives.Count > 0
            ? $"{hw.StorageDrives[0].TotalGB:F0} GB {hw.StorageDrives[0].Type}"
            : "Unknown";
        DisplaySummary = hw.PrimaryDisplay != null
            ? $"{hw.PrimaryDisplay.Resolution} @ {hw.PrimaryDisplay.RefreshRateHz} Hz"
            : "Unknown";
        WindowsVersion = $"{hw.WindowsVersion} (Build {hw.WindowsBuild})";
        IsLaptop = hw.IsLaptop;
        PowerPlan = hw.ActivePowerPlan;
        BatteryPercent = hw.BatteryPercent;
        IsOnBattery = hw.IsOnBattery;
    }

    private void StartMonitoring()
    {
        _monitorCts?.Cancel();
        _monitorCts = new CancellationTokenSource();
        var ct = _monitorCts.Token;

        // Capture the UI dispatcher from the current (UI) thread
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        _ = Task.Run(async () =>
        {
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await _timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    var m = await _hardware.GetLiveMetricsAsync(ct);
                    dispatcher?.TryEnqueue(() => UpdateMetrics(m));
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    App.Log($"[Monitor] {ex.Message}");
                }
            }
        }, ct);
    }

    private void UpdateMetrics(LiveMetrics m)
    {
        CpuUsage    = m.CpuUsagePercent;
        GpuUsage    = m.GpuUsagePercent;
        RamUsage    = m.RamUsagePercent;
        VramUsage   = m.VramUsagePercent;
        DiskActivity= m.DiskActivityPercent;
        CpuTemp     = m.CpuTempCelsius;
        GpuTemp     = m.GpuTempCelsius;
        CurrentFps  = m.CurrentFps;
        FrameTimeMs = m.FrameTimeMs;
        OnePercentLow = m.OnePercentLowFps;
        IsOnBattery = m.IsOnBattery;
        BatteryPercent = m.BatteryPercent;

        // Push real values into the shared performance history so every graph
        // (Dashboard, Performance page) shows real hardware readings.
        NEXORA.Design.MockPerformanceData.PushSample(
            m.CpuUsagePercent,
            m.GpuUsagePercent,
            m.RamUsagePercent);

        // Update live display fields in MockHardwareData so cards on other pages refresh.
        var hw = NEXORA.Design.MockHardwareData.Instance;
        hw.CpuUsage    = m.CpuUsagePercent;
        hw.CpuTemp     = m.CpuTempCelsius;
        hw.GpuUsage    = m.GpuUsagePercent;
        hw.GpuTemp     = m.GpuTempCelsius;
        hw.RamUsagePct = m.RamUsagePercent;
        hw.RamUsedGB   = m.RamUsedBytes / 1_073_741_824.0;
        hw.DiskActivity= m.DiskActivityPercent;
        hw.GpuVramUsed = m.VramUsedBytes / 1_073_741_824.0;
        NEXORA.Design.MockPerformanceData.DownloadMbps = m.NetworkDownloadMBps;
        NEXORA.Design.MockPerformanceData.UploadMbps   = m.NetworkUploadMBps;
    }

    public void Dispose()
    {
        _monitorCts?.Cancel();
        _timer?.Dispose();
    }
}
