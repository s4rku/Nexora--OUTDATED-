using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;
using System.Collections.ObjectModel;

namespace NEXORA.ViewModels;

public sealed partial class PcOptimizerViewModel : ObservableObject
{
    private readonly IOptimizationEngine _engine;
    private readonly IHardwareService _hardware;
    private readonly IRestoreService _restore;

    [ObservableProperty] private OptimizationLevel _selectedLevel = OptimizationLevel.Balanced;
    [ObservableProperty] private bool _isAnalyzing;
    [ObservableProperty] private bool _isOptimizing;
    [ObservableProperty] private bool _isRollingBack;
    [ObservableProperty] private string _statusMessage = "Select an optimization level and click Scan.";
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _progressMessage = string.Empty;
    [ObservableProperty] private OptimizationSession? _lastSession;
    [ObservableProperty] private bool _hasResults;

    public ObservableCollection<OptimizationDescriptor> Optimizations { get; } = new();

    public int SelectedCount => Optimizations.Count(o => o.IsSelected);
    public int SafeCount => Optimizations.Count(o => o.Risk == RiskLevel.Safe);

    public PcOptimizerViewModel(
        IOptimizationEngine engine,
        IHardwareService hardware,
        IRestoreService restore)
    {
        _engine = engine;
        _hardware = hardware;
        _restore = restore;
    }

    [RelayCommand]
    public async Task AnalyzeAsync()
    {
        IsAnalyzing = true;
        Optimizations.Clear();
        StatusMessage = "Analysing your system…";

        try
        {
            var hw = _hardware.GetCachedInfo() ?? await _hardware.ScanAsync();

            var opts = await _engine.AnalyzeAsync(
                SelectedLevel,
                hw,
                new Progress<string>(m => StatusMessage = m));

            foreach (var o in opts)
                Optimizations.Add(o);

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SafeCount));

            StatusMessage = Optimizations.Count == 0
                ? "Your system is already well-optimized for this level."
                : $"{Optimizations.Count} optimizations available. Review and apply.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    public async Task ApplySelectedAsync()
    {
        var selected = Optimizations.Where(o => o.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "No optimizations selected.";
            return;
        }

        IsOptimizing = true;
        ProgressValue = 0;
        StatusMessage = "Applying optimizations…";

        try
        {
            LastSession = await _engine.ApplyAsync(
                selected,
                SelectedLevel,
                new Progress<(string msg, int pct)>(p =>
                {
                    ProgressMessage = p.msg;
                    ProgressValue = p.pct;
                }));

            int succeeded = LastSession.AppliedOptimizations.Count(o => o.WasSuccessful);
            int failed = LastSession.AppliedOptimizations.Count(o => !o.WasSuccessful);

            HasResults = true;
            StatusMessage = failed == 0
                ? $"✓ {succeeded} optimizations applied successfully."
                : $"✓ {succeeded} applied, ⚠ {failed} failed. See details below.";

            // Refresh list
            await AnalyzeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Optimization failed: {ex.Message}";
        }
        finally
        {
            IsOptimizing = false;
            ProgressValue = 0;
        }
    }

    [RelayCommand]
    public async Task RollbackLastSessionAsync()
    {
        if (LastSession == null) return;
        IsRollingBack = true;
        StatusMessage = "Rolling back changes…";
        try
        {
            var ok = await _restore.RollbackSessionAsync(LastSession.Id);
            StatusMessage = ok
                ? "All changes have been rolled back successfully."
                : "Rollback partially completed. Some changes may require a restart.";
        }
        catch (Exception ex) { StatusMessage = $"Rollback failed: {ex.Message}"; }
        finally { IsRollingBack = false; }
    }

    public void ToggleAll(bool selected)
    {
        foreach (var o in Optimizations)
            o.IsSelected = selected;
        OnPropertyChanged(nameof(SelectedCount));
    }
}
