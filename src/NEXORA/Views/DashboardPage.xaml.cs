using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class DashboardPage : Page
{
    private DashboardViewModel _vm = null!;
    private bool _loaded;

    // Live graph state
    private readonly DispatcherTimer _graphTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private Canvas? _graphCanvas;

    // Named TextBlocks updated on every monitor tick
    private TextBlock? _cpuUsageTb, _cpuTempTb;
    private TextBlock? _gpuUsageTb, _gpuTempTb;
    private TextBlock? _ramUsageTb, _ramDetailTb;
    private TextBlock? _diskTb;
    private Border? _cpuFill, _gpuFill, _ramFill, _diskFill;

    public DashboardPage()
    {
        InitializeComponent();
        _graphTimer.Tick += (_, _) => RedrawGraph();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        SetGreeting();

        _vm = App.Services.GetRequiredService<DashboardViewModel>();
        _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(SyncFromVm);

        BuildPage();

        if (!_loaded)
        {
            _loaded = true;
            await _vm.LoadAsync();
        }
        _graphTimer.Start();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _graphTimer.Stop();
        _realDataBuilt = false; // allow rebuild on next navigation
    }

    // ── Greeting ──────────────────────────────────────────────────────────────

    private void SetGreeting()
    {
        var h = DateTime.Now.Hour;
        GreetingText.Text = h < 12 ? "Good morning." : h < 17 ? "Good afternoon." : "Good evening.";
    }

    // ── Page construction ─────────────────────────────────────────────────────

    private void BuildPage()
    {
        BuildScoreBars();
        BuildHardwareCards();
        BuildGraph();
        BuildOptimizeCard();
        BuildStatusRows();
    }

    // ── Score bars ────────────────────────────────────────────────────────────

    private void BuildScoreBars()
    {
        var hw = MockHardwareData.Instance;
        ScoreBars.Children.Clear();
        ScoreNum.Text   = hw.NexoraScore > 0 ? hw.NexoraScore.ToString() : "—";
        ScoreRing.Value = hw.NexoraScore;

        if (hw.NexoraScore == 0) return; // Wait for real scan
        AddScoreBar("CPU Performance",  hw.CpuScore,    Theme.Accent);
        AddScoreBar("GPU Performance",  hw.GpuScore,    Theme.AccentPurp);
        AddScoreBar("Memory",           hw.MemScore,    Theme.Success);
        AddScoreBar("Storage",          hw.StorageScore,Theme.Info);
        AddScoreBar("Gaming Readiness", hw.GamingScore, Theme.Warning);
    }

    private void AddScoreBar(string label, int score, Color color)
        => ScoreBars.Children.Add(NxScoreBar.Build(label, score, color));

    // ── Hardware cards ─────────────────────────────────────────────────────────

    private void BuildHardwareCards()
    {
        HardwareGrid.Children.Clear();
        var hw = MockHardwareData.Instance;

        // CPU card
        var cpuCard = MakeMetricCard("CPU", hw.CpuName,
            hw.CpuUsage, $"{hw.CpuUsage:F0}%", $"{hw.CpuTemp:F0}°C",
            Theme.Accent,
            out _cpuUsageTb, out _cpuTempTb, out _cpuFill);

        // GPU card
        var gpuCard = MakeMetricCard("GPU", hw.GpuName,
            hw.GpuUsage, $"{hw.GpuUsage:F0}%", $"{hw.GpuTemp:F0}°C",
            Theme.AccentPurp,
            out _gpuUsageTb, out _gpuTempTb, out _gpuFill);

        // RAM card
        var ramCard = MakeMetricCard("RAM",
            hw.RamTotalGB > 0 ? $"{hw.RamTotalGB:F0} GB  {hw.RamType}" : "—",
            hw.RamUsagePct, $"{hw.RamUsedGB:F1} GB used", null,
            Theme.Success,
            out _ramUsageTb, out var _ramT, out _ramFill);
        _ramDetailTb = _ramT;

        // Storage card
        var stoCard = MakeMetricCard("STORAGE",
            hw.StorageName.Length > 0 ? hw.StorageName : "—",
            hw.StorageUsagePct, $"{hw.StorageFreeGB:F0} GB free", null,
            Theme.Info,
            out _diskTb, out var _diskT, out _diskFill);

        Grid.SetColumn(cpuCard, 0); Grid.SetColumn(gpuCard, 1);
        Grid.SetColumn(ramCard, 2); Grid.SetColumn(stoCard, 3);
        HardwareGrid.Children.Add(cpuCard);
        HardwareGrid.Children.Add(gpuCard);
        HardwareGrid.Children.Add(ramCard);
        HardwareGrid.Children.Add(stoCard);
    }

    private static Border MakeMetricCard(
        string title, string subtitle,
        double pct, string primaryVal, string? secondaryVal,
        Color color,
        out TextBlock primaryTb, out TextBlock? secondaryTb, out Border barFill)
    {
        var panel = new StackPanel { Spacing = 10 };

        // Title row with accent dot
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        hdr.Children.Add(new Border
        {
            Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Center
        });
        hdr.Children.Add(Theme.Overline(title));
        panel.Children.Add(hdr);

        // Subtitle (device name)
        var subTb = Theme.Caption(subtitle);
        subTb.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
        subTb.MaxLines = 1;
        panel.Children.Add(subTb);

        // Primary value (big number)
        primaryTb = Theme.BigNum(primaryVal, color);
        primaryTb.FontSize = 30;
        panel.Children.Add(primaryTb);

        // Usage bar
        var barBg = new Border
        {
            Height = 5, CornerRadius = new CornerRadius(2.5),
            Background = new SolidColorBrush(Theme.Border)
        };
        barFill = new Border
        {
            Height = 5, CornerRadius = new CornerRadius(2.5),
            Background = new SolidColorBrush(color),
            HorizontalAlignment = HorizontalAlignment.Left, Width = 0
        };
        var barContainer = new Grid();
        barContainer.Children.Add(barBg);
        barContainer.Children.Add(barFill);
        var capturedFill = barFill;
        barContainer.SizeChanged += (_, _) =>
            capturedFill.Width = Math.Max(0, barContainer.ActualWidth * pct / 100.0);
        panel.Children.Add(barContainer);

        // Secondary value (temp / detail)
        if (secondaryVal != null)
        {
            secondaryTb = Theme.Caption(secondaryVal, Theme.TextMuted);
            panel.Children.Add(secondaryTb);
        }
        else secondaryTb = null;

        return NxCard.Metric(panel);
    }

    // ── Live graph ─────────────────────────────────────────────────────────────

    private void BuildGraph()
    {
        if (_graphCanvas == null) return;
        RedrawGraph();
    }

    private void RedrawGraph()
    {
        if (_graphCanvas == null) return;
        _graphCanvas.Children.Clear();
        double w = _graphCanvas.ActualWidth, h = 120;
        if (w < 10) return;
        DrawGridLines(w, h);
        DrawSeries(MockPerformanceData.CpuHistory, Theme.Accent,     w, h);
        DrawSeries(MockPerformanceData.GpuHistory, Theme.AccentPurp, w, h);
        DrawSeries(MockPerformanceData.RamHistory, Theme.Success,     w, h);
    }

    private void DrawGridLines(double w, double h)
    {
        for (int i = 1; i <= 3; i++)
        {
            double y = h * i / 4;
            _graphCanvas!.Children.Add(new Line
            {
                X1 = 0, X2 = w, Y1 = y, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
                StrokeThickness = 1
            });
        }
        // Y-axis labels
        foreach (var (pct, label) in new[] { (0,"100%"), (50,"50%"), (99,"0%") })
        {
            var tb = Theme.Caption(label, Theme.TextMuted);
            tb.FontSize = 9;
            Canvas.SetTop(tb, h * pct / 100 - 5);
            Canvas.SetLeft(tb, 2);
            _graphCanvas!.Children.Add(tb);
        }
    }

    private void DrawSeries(List<double> data, Color color, double w, double h)
    {
        if (data.Count < 2) return;
        double step = w / (data.Count - 1);
        var area = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromArgb(0x14, color.R, color.G, color.B)),
            StrokeThickness = 0
        };
        var line = new Polyline
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.5,
            StrokeLineJoin = Microsoft.UI.Xaml.Media.PenLineJoin.Round
        };
        area.Points.Add(new Windows.Foundation.Point(0, h));
        for (int i = 0; i < data.Count; i++)
        {
            var pt = new Windows.Foundation.Point(i * step, h - data[i] / 100.0 * h);
            line.Points.Add(pt);
            area.Points.Add(pt);
        }
        area.Points.Add(new Windows.Foundation.Point((data.Count - 1) * step, h));
        _graphCanvas!.Children.Add(area);
        _graphCanvas.Children.Add(line);
    }

    // ── Optimize card ──────────────────────────────────────────────────────────

    private void BuildOptimizeCard()
    {
        // Handled by XAML bindings — OptimizeReadyText updated in SyncFromVm
    }

    // ── System status ──────────────────────────────────────────────────────────

    private void BuildStatusRows()
    {
        StatusRows.Children.Clear();
        var hw = MockHardwareData.Instance;

        // Status is derived from real score data once available
        bool hasData = hw.NexoraScore > 0;
        AddStatusRow("CPU",    hasData ? (hw.CpuScore >= 70 ? "Healthy" : "Limited")     : "Scanning…",  hasData && hw.CpuScore >= 70);
        AddStatusRow("GPU",    hasData ? (hw.GpuScore >= 60 ? "Healthy" : "Limited")     : "Scanning…",  hasData && hw.GpuScore >= 60);
        AddStatusRow("Memory", hasData ? (hw.MemScore >= 70 ? "Sufficient" : "Low")      : "Scanning…",  hasData && hw.MemScore >= 70);
        AddStatusRow("Storage",hasData ? (hw.StorageScore >= 70 ? "Healthy" : "Limited") : "Scanning…",  hasData && hw.StorageScore >= 70);
        AddStatusRow("Power Plan", hw.PowerPlan.Length > 0 ? hw.PowerPlan : "—", hw.PowerPlan == "High Performance");
    }

    private void AddStatusRow(string label, string status, bool good)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        left.Children.Add(new Ellipse
        {
            Width = 6, Height = 6,
            Fill = new SolidColorBrush(good ? Theme.Success : Theme.Warning),
            VerticalAlignment = VerticalAlignment.Center
        });
        left.Children.Add(Theme.Body(label));
        Grid.SetColumn(left, 0);

        var right = Theme.Caption(status, good ? Theme.Success : Theme.Warning);
        right.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(right, 1);

        row.Children.Add(left);
        row.Children.Add(right);
        StatusRows.Children.Add(row);
    }

    // ── VM sync — called whenever DashboardViewModel properties change ─────────

    private bool _realDataBuilt;

    private void SyncFromVm()
    {
        if (_vm == null) return;

        // Live metric values update every 2s
        if (_cpuUsageTb != null) _cpuUsageTb.Text = $"{_vm.CpuUsage:F0}%";
        if (_cpuTempTb  != null) _cpuTempTb.Text  = _vm.CpuTemp > 0 ? $"{_vm.CpuTemp:F0}°C" : "—";
        if (_gpuUsageTb != null) _gpuUsageTb.Text = $"{_vm.GpuUsage:F0}%";
        if (_gpuTempTb  != null) _gpuTempTb.Text  = _vm.GpuTemp > 0 ? $"{_vm.GpuTemp:F0}°C" : "—";
        if (_ramUsageTb != null) _ramUsageTb.Text  = $"{_vm.RamUsage:F0}%";
        if (_diskTb     != null) _diskTb.Text      = $"{_vm.DiskActivity:F0}%";

        UpdateBar(_cpuFill, _vm.CpuUsage);
        UpdateBar(_gpuFill, _vm.GpuUsage);
        UpdateBar(_ramFill, _vm.RamUsage);
        UpdateBar(_diskFill, _vm.DiskActivity);

        // Push real samples into graph ring buffer
        MockPerformanceData.PushSample(_vm.CpuUsage, _vm.GpuUsage, _vm.RamUsage);

        // Optimize button
        if (_vm.AvailableOptimizations > 0)
        {
            OptimizeReadyText.Text   = $"NEXORA found {_vm.AvailableOptimizations} recommended optimizations for your system.";
            OptimizeNowBtn.IsEnabled = true;
        }

        // Rebuild score/hardware cards exactly once after the real scan completes
        var hw = MockHardwareData.Instance;
        bool scanDone = !string.IsNullOrEmpty(_vm.CpuName) && _vm.CpuName != "—" && hw.NexoraScore > 0;
        if (scanDone && !_realDataBuilt)
        {
            _realDataBuilt = true;
            BuildScoreBars();
            BuildHardwareCards();
            BuildStatusRows();
        }
    }

    private static void UpdateBar(Border? fill, double pct)
    {
        if (fill?.Parent is Grid g)
            fill.Width = Math.Max(0, g.ActualWidth * pct / 100.0);
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void PerfGraph_Loaded(object sender, RoutedEventArgs e)
    {
        _graphCanvas = PerfGraph;
        _graphCanvas.SizeChanged += (_, _) => RedrawGraph();
        RedrawGraph();
    }

    private void ScanBtn_Click(object sender, RoutedEventArgs e) => _ = _vm?.LoadAsync();

    private void OptimizeNow_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mw)
            mw.Navigate("PcOptimizer", mw.NavPcOptimizerBtn);
    }

    private async void ViewDetails_Click(object sender, RoutedEventArgs e)
        => await NxModal.ShowComingSoon(XamlRoot, "Optimization details");
}
