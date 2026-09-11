using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class BenchmarkPage : Page
{
    private BenchmarkViewModel? _vm;
    private StackPanel? _resultsPanel;

    public BenchmarkPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            _vm = App.Services.GetRequiredService<BenchmarkViewModel>();
            _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(RefreshResults);
            Build();
        }
        catch (Exception ex) { App.Log($"BenchmarkPage crash: {ex}"); }
    }

    private void Build()
    {
        Root.Children.Clear();

        Root.Children.Add(NxSection.Header("Benchmark",
            "Capture performance snapshots before and after optimization to measure the difference."));

        // Score rings — show real score from hardware scan when available
        var hw = MockHardwareData.Instance;
        bool hasScore = hw.NexoraScore > 0;

        Root.Children.Add(Theme.Overline("NEXORA PERFORMANCE SCORES"));
        var ringsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        ringsPanel.Children.Add(ScoreRingCol(hasScore ? hw.NexoraScore  : 0, "Overall", Theme.Accent, 130));
        ringsPanel.Children.Add(ScoreRingCol(hasScore ? hw.CpuScore     : 0, "CPU",     Theme.Info));
        ringsPanel.Children.Add(ScoreRingCol(hasScore ? hw.GpuScore     : 0, "GPU",     Theme.AccentPurp));
        ringsPanel.Children.Add(ScoreRingCol(hasScore ? hw.MemScore     : 0, "Memory",  Theme.Success));
        ringsPanel.Children.Add(ScoreRingCol(hasScore ? hw.GamingScore  : 0, "Gaming",  Theme.Warning));
        Root.Children.Add(NxCard.Hero(ringsPanel));

        if (!hasScore)
            Root.Children.Add(Theme.Caption("Scores will appear after completing a system scan on the Dashboard.", Theme.TextMuted));

        // Before / After snapshot capture
        Root.Children.Add(Theme.Overline("BEFORE / AFTER COMPARISON"));
        var capturePanel = new StackPanel { Spacing = 12 };
        capturePanel.Children.Add(Theme.Body(
            "Capture a baseline snapshot, apply optimizations, then capture again to see the improvement.", Theme.TextSec));

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var beforeBtn = new Button { Content = "CAPTURE BEFORE", Style = (Style)Application.Current.Resources["NxOutlineBtn"] };
        beforeBtn.Click += async (_, _) => { try { if (_vm != null) await _vm.CaptureBeforeAsync(); } catch { } };
        var afterBtn = new Button { Content = "CAPTURE AFTER", Style = (Style)Application.Current.Resources["NxPrimaryBtn"] };
        afterBtn.Click += async (_, _) => { try { if (_vm != null) await _vm.CaptureAfterAsync(); } catch { } };
        btnRow.Children.Add(beforeBtn);
        btnRow.Children.Add(afterBtn);
        capturePanel.Children.Add(btnRow);
        Root.Children.Add(NxCard.Standard(capturePanel));

        // Results area
        _resultsPanel = new StackPanel { Spacing = 8 };
        Root.Children.Add(_resultsPanel);

        // Start full benchmark
        Root.Children.Add(Theme.Overline("FULL BENCHMARK SUITE"));
        var suitePanel = new StackPanel { Spacing = 10 };
        suitePanel.Children.Add(Theme.Body("Run a full synthetic benchmark across CPU, GPU, memory, and storage."));
        var startBtn = new Button { Content = "START BENCHMARK", Style = (Style)Application.Current.Resources["NxPrimaryBtn"] };
        startBtn.Click += async (_, _) => { try { await NxModal.ShowComingSoon(XamlRoot, "Full Benchmark Suite"); } catch { } };
        suitePanel.Children.Add(startBtn);
        Root.Children.Add(NxCard.Standard(suitePanel));
    }

    private void RefreshResults()
    {
        if (_vm == null || _resultsPanel == null) return;
        _resultsPanel.Children.Clear();

        if (_vm.SnapshotBefore == null && _vm.SnapshotAfter == null) return;

        if (_vm.SnapshotBefore != null && _vm.SnapshotAfter == null)
        {
            _resultsPanel.Children.Add(NxCard.Standard(new StackPanel { Spacing = 6, Children =
            {
                Theme.Overline("BASELINE CAPTURED"),
                Theme.Body("Apply your optimizations, then click CAPTURE AFTER to compare."),
                Theme.Caption($"Captured at {_vm.SnapshotBefore.CapturedAt:HH:mm:ss}", Theme.TextMuted)
            }}));
            return;
        }

        if (_vm.SnapshotBefore != null && _vm.SnapshotAfter != null)
        {
            var compPanel = new StackPanel { Spacing = 12 };
            compPanel.Children.Add(Theme.Overline("COMPARISON RESULTS"));

            // Before / After columns
            var cols = new Grid { ColumnSpacing = 20 };
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(SnapshotCol("Before", _vm.SnapshotBefore), 0);
            Grid.SetColumn(SnapshotCol("After",  _vm.SnapshotAfter),  1);
            Grid.SetColumn(DeltaCol(_vm), 2);
            cols.Children.Add(SnapshotCol("Before", _vm.SnapshotBefore));
            cols.Children.Add(SnapshotCol("After",  _vm.SnapshotAfter));
            cols.Children.Add(DeltaCol(_vm));
            compPanel.Children.Add(cols);
            _resultsPanel.Children.Add(NxCard.Standard(compPanel));
        }
    }

    private static StackPanel SnapshotCol(string label, NEXORA.Core.Models.PerformanceSnapshot snap)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(Theme.Overline(label));
        sp.Children.Add(StatLine("CPU",     $"{snap.CpuUsagePercent:F0}%", Theme.Accent));
        sp.Children.Add(StatLine("GPU",     $"{snap.GpuUsagePercent:F0}%", Theme.AccentPurp));
        sp.Children.Add(StatLine("RAM",     $"{snap.RamUsagePercent:F0}%", Theme.Success));
        sp.Children.Add(StatLine("CPU °C",  $"{snap.CpuTempCelsius:F0}",   Theme.Warning));
        sp.Children.Add(StatLine("GPU °C",  $"{snap.GpuTempCelsius:F0}",   Theme.Warning));
        return sp;
    }

    private static StackPanel DeltaCol(BenchmarkViewModel vm)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(Theme.Overline("Change"));
        if (vm.FpsImprovement != 0)
            sp.Children.Add(DeltaLine("FPS", vm.FpsImprovement));
        if (vm.LowFpsImprovement != 0)
            sp.Children.Add(DeltaLine("1% Low", vm.LowFpsImprovement));
        return sp;
    }

    private static StackPanel StatLine(string label, string val, Color color)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        sp.Children.Add(Theme.Caption(label));
        sp.Children.Add(Theme.Body(val, color));
        return sp;
    }

    private static StackPanel DeltaLine(string label, double pct)
    {
        var color = pct >= 0 ? Theme.Success : Theme.Danger;
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        sp.Children.Add(Theme.Caption(label));
        sp.Children.Add(Theme.Body($"{pct:+0.0;-0.0}%", color));
        return sp;
    }

    private static StackPanel ScoreRingCol(int score, string label, Color color, double size = 100)
    {
        var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6 };
        col.Children.Add(NxProgressRing.Build(score, 100, label, size, color));
        return col;
    }
}
