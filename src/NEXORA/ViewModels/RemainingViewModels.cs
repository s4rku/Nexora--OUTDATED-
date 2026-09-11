using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXORA.Core.AI;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;
using NEXORA.Core.Network;
using NEXORA.Core.Storage;
using NEXORA.Core.SystemTools;
using System.Collections.ObjectModel;

namespace NEXORA.ViewModels;

// ── Benchmark ────────────────────────────────────────────────────────────────

public sealed partial class BenchmarkViewModel : ObservableObject
{
    private readonly IHardwareService _hardware;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private PerformanceSnapshot? _snapshotBefore;
    [ObservableProperty] private PerformanceSnapshot? _snapshotAfter;
    [ObservableProperty] private string _statusMessage = "Record a snapshot before and after optimization to compare.";
    [ObservableProperty] private double _fpsImprovement;
    [ObservableProperty] private double _lowFpsImprovement;

    public BenchmarkViewModel(IHardwareService hardware) { _hardware = hardware; }

    [RelayCommand]
    public async Task CaptureBeforeAsync()
    {
        IsRunning = true;
        StatusMessage = "Capturing baseline snapshot…";
        try
        {
            var m = await _hardware.GetLiveMetricsAsync();
            SnapshotBefore = new PerformanceSnapshot
            {
                Label = "Before",
                CapturedAt = DateTime.UtcNow,
                CpuUsagePercent = m.CpuUsagePercent,
                GpuUsagePercent = m.GpuUsagePercent,
                RamUsagePercent = m.RamUsagePercent,
                CpuTempCelsius = m.CpuTempCelsius,
                GpuTempCelsius = m.GpuTempCelsius,
                AverageFps = m.AverageFps,
                OnePercentLowFps = m.OnePercentLowFps,
                FrameTimeMs = m.FrameTimeMs
            };
            StatusMessage = "Baseline captured. Apply optimizations, then capture the After snapshot.";
        }
        finally { IsRunning = false; }
    }

    [RelayCommand]
    public async Task CaptureAfterAsync()
    {
        if (SnapshotBefore == null) { StatusMessage = "Capture the Before snapshot first."; return; }
        IsRunning = true;
        StatusMessage = "Capturing after snapshot…";
        try
        {
            var m = await _hardware.GetLiveMetricsAsync();
            SnapshotAfter = new PerformanceSnapshot
            {
                Label = "After",
                CapturedAt = DateTime.UtcNow,
                CpuUsagePercent = m.CpuUsagePercent,
                GpuUsagePercent = m.GpuUsagePercent,
                RamUsagePercent = m.RamUsagePercent,
                CpuTempCelsius = m.CpuTempCelsius,
                GpuTempCelsius = m.GpuTempCelsius,
                AverageFps = m.AverageFps,
                OnePercentLowFps = m.OnePercentLowFps,
                FrameTimeMs = m.FrameTimeMs
            };

            if (SnapshotBefore.AverageFps > 0 && SnapshotAfter.AverageFps > 0)
                FpsImprovement = (SnapshotAfter.AverageFps - SnapshotBefore.AverageFps) / SnapshotBefore.AverageFps * 100;
            if (SnapshotBefore.OnePercentLowFps > 0 && SnapshotAfter.OnePercentLowFps > 0)
                LowFpsImprovement = (SnapshotAfter.OnePercentLowFps - SnapshotBefore.OnePercentLowFps) / SnapshotBefore.OnePercentLowFps * 100;

            StatusMessage = "Comparison complete.";
        }
        finally { IsRunning = false; }
    }
}

// ── Startup Manager ──────────────────────────────────────────────────────────

public sealed partial class StartupManagerViewModel : ObservableObject
{
    private readonly StartupManagerService _service;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "Loading startup entries…";

    public ObservableCollection<StartupEntry> Entries { get; } = new();

    public StartupManagerViewModel(StartupManagerService service) { _service = service; }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        Entries.Clear();
        try
        {
            var items = await _service.GetStartupEntriesAsync();
            foreach (var e in items) Entries.Add(e);
            StatusMessage = $"{Entries.Count} startup entries found.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task ToggleEntryAsync(StartupEntry entry)
    {
        await _service.SetEnabledAsync(entry, !entry.IsEnabled);
        await LoadAsync();
    }
}

// ── Process Manager ──────────────────────────────────────────────────────────

public sealed partial class ProcessManagerViewModel : ObservableObject
{
    private readonly ProcessMonitorService _service;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<ProcessEntry> Processes { get; } = new();

    public ProcessManagerViewModel(ProcessMonitorService service) { _service = service; }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        Processes.Clear();
        try
        {
            var procs = await _service.GetProcessesAsync();
            foreach (var p in procs) Processes.Add(p);
            StatusMessage = $"{Processes.Count} processes running.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }
}

// ── Storage Cleaner ──────────────────────────────────────────────────────────

public sealed partial class StorageCleanerViewModel : ObservableObject
{
    private readonly StorageCleanerService _service;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isCleaning;
    [ObservableProperty] private string _statusMessage = "Click Scan to find cleanable files.";
    [ObservableProperty] private long _totalReclaimableBytes;
    [ObservableProperty] private string _totalReclaimable = "—";

    public ObservableCollection<CleanupCategory> Categories { get; } = new();

    public StorageCleanerViewModel(StorageCleanerService service) { _service = service; }

    [RelayCommand]
    public async Task ScanAsync()
    {
        IsScanning = true;
        Categories.Clear();
        StatusMessage = "Scanning…";
        try
        {
            var cats = await _service.ScanAsync(new Progress<string>(m => StatusMessage = m));
            foreach (var c in cats) Categories.Add(c);
            TotalReclaimableBytes = Categories.Where(c => c.IsSelected).Sum(c => c.ReclaimableBytes);
            TotalReclaimable = $"{TotalReclaimableBytes / 1_048_576.0:F1} MB";
            StatusMessage = $"Scan complete. {TotalReclaimable} can be safely removed.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsScanning = false; }
    }

    [RelayCommand]
    public async Task CleanAsync()
    {
        IsCleaning = true;
        StatusMessage = "Cleaning…";
        try
        {
            var freed = await _service.CleanAsync(Categories.ToList(),
                new Progress<(string msg, int pct)>(p => StatusMessage = p.msg));
            StatusMessage = $"Cleaned {freed / 1_048_576.0:F1} MB.";
            await ScanAsync();
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsCleaning = false; }
    }
}

// ── Network ──────────────────────────────────────────────────────────────────

public sealed partial class NetworkViewModel : ObservableObject
{
    private readonly NetworkDiagnosticsService _service;

    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private NetworkDiagnostics? _results;
    [ObservableProperty] private string _statusMessage = "Click Test to run network diagnostics.";

    public NetworkViewModel(NetworkDiagnosticsService service) { _service = service; }

    [RelayCommand]
    public async Task RunTestAsync()
    {
        IsTesting = true;
        StatusMessage = "Running diagnostics…";
        try
        {
            Results = await _service.RunDiagnosticsAsync(new Progress<string>(m => StatusMessage = m));
            StatusMessage = "Diagnostics complete.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsTesting = false; }
    }
}

// ── Restore Center ────────────────────────────────────────────────────────────

public sealed partial class RestoreCenterViewModel : ObservableObject
{
    private readonly IRestoreService _restore;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isRollingBack;
    [ObservableProperty] private string _statusMessage = "Loading restore history…";

    public ObservableCollection<OptimizationSession> Sessions { get; } = new();

    public RestoreCenterViewModel(IRestoreService restore) { _restore = restore; }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        Sessions.Clear();
        try
        {
            var sessions = await _restore.GetSessionsAsync();
            foreach (var s in sessions) Sessions.Add(s);
            StatusMessage = Sessions.Count == 0
                ? "No optimization history found."
                : $"{Sessions.Count} sessions in history.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task RollbackSessionAsync(OptimizationSession session)
    {
        IsRollingBack = true;
        StatusMessage = $"Rolling back session from {session.AppliedAt:g}…";
        try
        {
            var ok = await _restore.RollbackSessionAsync(session.Id);
            StatusMessage = ok ? "Rollback complete." : "Rollback partially complete.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = $"Rollback failed: {ex.Message}"; }
        finally { IsRollingBack = false; }
    }
}

// ── Settings ──────────────────────────────────────────────────────────────────

public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly string _settingsPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NEXORA", "settings.json");

    [ObservableProperty] private bool _telemetryEnabled;
    [ObservableProperty] private bool _autoGameLaunch;
    [ObservableProperty] private bool _backgroundMonitoring;
    [ObservableProperty] private bool _showFpsOverlay;
    [ObservableProperty] private string _appVersion = "1.0.0";

    public SettingsViewModel()
    {
        Load();
        // Auto-save whenever any property changes
        PropertyChanged += (_, _) => Save();
    }

    private void Load()
    {
        try
        {
            if (!System.IO.File.Exists(_settingsPath)) return;
            var json = System.IO.File.ReadAllText(_settingsPath);
            var d    = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
            if (d == null) return;
            if (d.TryGetValue("telemetry",   out var t)) TelemetryEnabled    = t;
            if (d.TryGetValue("autoLaunch",  out var a)) AutoGameLaunch      = a;
            if (d.TryGetValue("bgMonitor",   out var b)) BackgroundMonitoring = b;
            if (d.TryGetValue("fpsOverlay",  out var f)) ShowFpsOverlay      = f;
        }
        catch { /* use defaults */ }
    }

    private void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_settingsPath)!);
            var d = new Dictionary<string, bool>
            {
                ["telemetry"]  = TelemetryEnabled,
                ["autoLaunch"] = AutoGameLaunch,
                ["bgMonitor"]  = BackgroundMonitoring,
                ["fpsOverlay"] = ShowFpsOverlay
            };
            System.IO.File.WriteAllText(_settingsPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(d, Newtonsoft.Json.Formatting.Indented));
        }
        catch { /* ignore save failures */ }
    }
}

// ── AI Advisor ────────────────────────────────────────────────────────────────

public sealed partial class AIAdvisorViewModel : ObservableObject
{
    private readonly NexoraAIAdvisor _advisor;
    private readonly IHardwareService _hardware;

    [ObservableProperty] private string _userInput = string.Empty;
    [ObservableProperty] private string _advisorResponse = "Hello. I'm NEXORA AI. Ask me about your system performance, FPS, temperatures, or bottlenecks.";
    [ObservableProperty] private bool _isThinking;
    [ObservableProperty] private AIAnalysisResult? _currentAnalysis;

    public ObservableCollection<ChatMessage> ChatHistory { get; } = new();

    public AIAdvisorViewModel(NexoraAIAdvisor advisor, IHardwareService hardware)
    {
        _advisor  = advisor;
        _hardware = hardware;
        // Seed greeting so it persists across RefreshChat rebuilds
        ChatHistory.Add(new ChatMessage
        {
            IsUser    = false,
            Text      = "Hello. I'm NEXORA AI.\n\nAsk me why your FPS is low, what's bottlenecking your system, or click 'Analyse System Now' for an instant assessment.",
            Timestamp = DateTime.Now
        });
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput)) return;

        var question = UserInput.Trim();
        UserInput = string.Empty;
        IsThinking = true;

        ChatHistory.Add(new ChatMessage { IsUser = true, Text = question, Timestamp = DateTime.Now });

        try
        {
            var hw = _hardware.GetCachedInfo() ?? await _hardware.ScanAsync();
            var metrics = await _hardware.GetLiveMetricsAsync();
            var answer = _advisor.AnswerQuestion(question, metrics, hw);

            ChatHistory.Add(new ChatMessage { IsUser = false, Text = answer, Timestamp = DateTime.Now });
            AdvisorResponse = answer;
        }
        catch (Exception ex)
        {
            ChatHistory.Add(new ChatMessage { IsUser = false, Text = $"Error: {ex.Message}", Timestamp = DateTime.Now });
        }
        finally { IsThinking = false; }
    }

    [RelayCommand]
    public async Task AnalyzeNowAsync()
    {
        IsThinking = true;
        try
        {
            var hw = _hardware.GetCachedInfo() ?? await _hardware.ScanAsync();
            var metrics = await _hardware.GetLiveMetricsAsync();
            CurrentAnalysis = _advisor.AnalyzePerformance(metrics, hw);
            AdvisorResponse = CurrentAnalysis.PrimaryInsight;
            ChatHistory.Add(new ChatMessage
            {
                IsUser = false,
                Text = CurrentAnalysis.PrimaryInsight,
                Timestamp = DateTime.Now
            });
        }
        finally { IsThinking = false; }
    }
}

public sealed class ChatMessage
{
    public bool IsUser { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
