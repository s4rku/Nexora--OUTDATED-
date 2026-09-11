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

public sealed partial class PerformancePage : Page
{
    private DashboardViewModel? _vm;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    // Named refs for live update
    private TextBlock? _cpuValTb, _gpuValTb, _ramValTb, _diskValTb;
    private TextBlock? _cpuTempTb, _gpuTempTb;
    private Canvas? _mainCanvas, _fpsCanvas;
    private Border? _cpuFill, _gpuFill, _ramFill, _diskFill;

    public PerformancePage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _vm = App.Services.GetRequiredService<DashboardViewModel>();
        _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(SyncLive);

        try { Build(); }
        catch (Exception ex) { App.Log($"PerformancePage crash: {ex}"); }

        _timer.Tick += (_, _) => { RedrawMain(); RedrawFps(); };
        _timer.Start();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _timer.Stop();
    }

    private void Build()
    {
        Root.Children.Clear();

        // Header
        Root.Children.Add(NxSection.Header("Performance", "Live system monitoring — updates every 2 seconds."));

        // 6-column live stats row
        var grid = new Grid { ColumnSpacing = 10 };
        for (int i = 0; i < 6; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var hw = MockHardwareData.Instance;

        // CPU
        var (cpuCard, cpuV, cpuT, cpuF) = MakeStatCard("CPU", hw.CpuName, $"{hw.CpuUsage:F0}%", $"{hw.CpuTemp:F0}°C", Theme.Accent, hw.CpuUsage);
        _cpuValTb = cpuV; _cpuTempTb = cpuT; _cpuFill = cpuF;

        // GPU
        var (gpuCard, gpuV, gpuT, gpuF) = MakeStatCard("GPU", hw.GpuName, $"{hw.GpuUsage:F0}%", $"{hw.GpuTemp:F0}°C", Theme.AccentPurp, hw.GpuUsage);
        _gpuValTb = gpuV; _gpuTempTb = gpuT; _gpuFill = gpuF;

        // RAM
        var (ramCard, ramV, ramT, ramF) = MakeStatCard("RAM", $"{hw.RamTotalGB:F0} GB", $"{hw.RamUsagePct:F0}%", $"{hw.RamUsedGB:F1} GB used", Theme.Success, hw.RamUsagePct);
        _ramValTb = ramV; _ramFill = ramF;

        // VRAM
        var vramPct = hw.GpuVramGB > 0 ? hw.GpuVramUsed / hw.GpuVramGB * 100 : 0;
        var (vramCard, vramV, _, vramF) = MakeStatCard("VRAM", $"{hw.GpuVramGB:F0} GB total", $"{hw.GpuVramUsed:F1} GB", "Used", Theme.Warning, vramPct);

        // Disk
        var (diskCard, diskV, _, diskF) = MakeStatCard("DISK", "Activity", $"{hw.DiskActivity:F0}%", "I/O activity", Theme.Info, hw.DiskActivity);
        _diskValTb = diskV; _diskFill = diskF;

        // Network
        var (netCard, netV, _, _) = MakeStatCard("NETWORK", "Throughput", $"{MockPerformanceData.DownloadMbps:F0}", "Mbps down", Theme.Danger, 40);

        int col = 0;
        foreach (var c in new[] { cpuCard, gpuCard, ramCard, vramCard, diskCard, netCard })
        {
            Grid.SetColumn(c, col++);
            grid.Children.Add(c);
        }
        Root.Children.Add(grid);

        // Main graph
        Root.Children.Add(Theme.Overline("CPU / GPU / RAM  —  LAST 2 MINUTES"));
        _mainCanvas = new Canvas { Height = 160 };
        _mainCanvas.SizeChanged += (_, _) => RedrawMain();
        Root.Children.Add(NxCard.Standard(_mainCanvas));

        // FPS row
        var fpsRow = new Grid { ColumnSpacing = 12 };
        fpsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fpsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // FPS stats
        var fpsPanel = new StackPanel { Spacing = 12 };
        fpsPanel.Children.Add(Theme.Overline("FRAME RATE"));
        var fpsGrid = new Grid { ColumnSpacing = 16 };
        fpsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fpsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fpsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var fpsStat  = MiniStat("Average FPS", "—",   Theme.Accent);
        var lowStat  = MiniStat("1% Low",      "—",   Theme.AccentPurp);
        var ftStat   = MiniStat("Frame Time",  "—",   Theme.Warning);
        Grid.SetColumn(fpsStat, 0); Grid.SetColumn(lowStat, 1); Grid.SetColumn(ftStat, 2);
        fpsGrid.Children.Add(fpsStat); fpsGrid.Children.Add(lowStat); fpsGrid.Children.Add(ftStat);
        fpsPanel.Children.Add(fpsGrid);
        fpsPanel.Children.Add(Theme.Caption("Open a game to begin frame-time monitoring.", Theme.TextMuted));
        fpsRow.Children.Add(NxCard.Standard(fpsPanel));

        // FPS graph
        _fpsCanvas = new Canvas { Height = 110 };
        _fpsCanvas.SizeChanged += (_, _) => RedrawFps();
        fpsRow.Children.Add(NxCard.Standard(_fpsCanvas));
        Root.Children.Add(fpsRow);
    }

    private static (Border card, TextBlock valTb, TextBlock? tempTb, Border fill)
        MakeStatCard(string title, string subtitle, string val, string sub2, Color color, double pct)
    {
        var panel = new StackPanel { Spacing = 8 };

        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        hdr.Children.Add(new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center });
        hdr.Children.Add(Theme.Overline(title));
        panel.Children.Add(hdr);

        var subTb = Theme.Caption(subtitle);
        subTb.MaxLines = 1;
        subTb.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
        panel.Children.Add(subTb);

        var valTb = Theme.BigNum(val, color); valTb.FontSize = 26;
        panel.Children.Add(valTb);

        // Bar
        var barBg = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Border) };
        var fill   = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(color), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        var bg = new Grid(); bg.Children.Add(barBg); bg.Children.Add(fill);
        var cf = fill;
        bg.SizeChanged += (_, _) => cf.Width = Math.Max(0, bg.ActualWidth * pct / 100.0);
        panel.Children.Add(bg);

        var sub2Tb = Theme.Caption(sub2, Theme.TextMuted);
        panel.Children.Add(sub2Tb);

        return (NxCard.Metric(panel), valTb, sub2Tb, fill);
    }

    private static StackPanel MiniStat(string label, string val, Color color)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(Theme.Caption(label));
        sp.Children.Add(Theme.H2(val, color));
        return sp;
    }

    // ── Live sync from DashboardViewModel ─────────────────────────────────────

    private void SyncLive()
    {
        if (_vm == null) return;
        if (_cpuValTb != null) _cpuValTb.Text = $"{_vm.CpuUsage:F0}%";
        if (_cpuTempTb != null) _cpuTempTb.Text = _vm.CpuTemp > 0 ? $"{_vm.CpuTemp:F0}°C" : "—";
        if (_gpuValTb != null) _gpuValTb.Text = $"{_vm.GpuUsage:F0}%";
        if (_gpuTempTb != null) _gpuTempTb.Text = _vm.GpuTemp > 0 ? $"{_vm.GpuTemp:F0}°C" : "—";
        if (_ramValTb != null) _ramValTb.Text = $"{_vm.RamUsage:F0}%";
        if (_diskValTb != null) _diskValTb.Text = $"{_vm.DiskActivity:F0}%";
        UpdateFill(_cpuFill, _vm.CpuUsage);
        UpdateFill(_gpuFill, _vm.GpuUsage);
        UpdateFill(_ramFill, _vm.RamUsage);
        UpdateFill(_diskFill, _vm.DiskActivity);
    }

    private static void UpdateFill(Border? fill, double pct)
    {
        if (fill?.Parent is Grid g) fill.Width = Math.Max(0, g.ActualWidth * pct / 100.0);
    }

    // ── Graphs ────────────────────────────────────────────────────────────────

    private void RedrawMain()
    {
        if (_mainCanvas == null) return;
        _mainCanvas.Children.Clear();
        double w = _mainCanvas.ActualWidth, h = 160;
        if (w < 10) return;
        DrawGrid(_mainCanvas, w, h);
        DrawLine(_mainCanvas, MockPerformanceData.CpuHistory, Theme.Accent,     w, h);
        DrawLine(_mainCanvas, MockPerformanceData.GpuHistory, Theme.AccentPurp, w, h);
        DrawLine(_mainCanvas, MockPerformanceData.RamHistory, Theme.Success,     w, h);

        // Legend
        DrawLegendDot(_mainCanvas, Theme.Accent,     "CPU",  12, 8);
        DrawLegendDot(_mainCanvas, Theme.AccentPurp, "GPU",  60, 8);
        DrawLegendDot(_mainCanvas, Theme.Success,    "RAM", 108, 8);
    }

    private void RedrawFps()
    {
        if (_fpsCanvas == null) return;
        _fpsCanvas.Children.Clear();
        double w = _fpsCanvas.ActualWidth, h = 110;
        if (w < 10) return;
        DrawGrid(_fpsCanvas, w, h);
        DrawLine(_fpsCanvas, MockPerformanceData.FpsHistory, Theme.Accent, w, h, 200);
    }

    private static void DrawGrid(Canvas c, double w, double h)
    {
        for (int i = 1; i <= 3; i++)
        {
            double y = h * i / 4;
            c.Children.Add(new Line { X1=0, X2=w, Y1=y, Y2=y, Stroke=new SolidColorBrush(Color.FromArgb(0x10,0xFF,0xFF,0xFF)), StrokeThickness=1 });
        }
    }

    private static void DrawLine(Canvas c, List<double> data, Color color, double w, double h, double max = 100)
    {
        if (data.Count < 2) return;
        double step = w / (data.Count - 1);
        var area = new Polygon { Fill = new SolidColorBrush(Color.FromArgb(0x14, color.R, color.G, color.B)), StrokeThickness = 0 };
        var line = new Polyline { Stroke = new SolidColorBrush(color), StrokeThickness = 1.8, StrokeLineJoin = Microsoft.UI.Xaml.Media.PenLineJoin.Round };
        area.Points.Add(new Windows.Foundation.Point(0, h));
        for (int i = 0; i < data.Count; i++)
        {
            double x = i * step, y = h - data[i] / max * h;
            line.Points.Add(new Windows.Foundation.Point(x, y));
            area.Points.Add(new Windows.Foundation.Point(x, y));
        }
        area.Points.Add(new Windows.Foundation.Point((data.Count - 1) * step, h));
        c.Children.Add(area); c.Children.Add(line);
    }

    private static void DrawLegendDot(Canvas c, Color color, string label, double x, double y)
    {
        var dot = new Ellipse { Width=7, Height=7, Fill=new SolidColorBrush(color) };
        Canvas.SetLeft(dot, x); Canvas.SetTop(dot, y + 1);
        c.Children.Add(dot);
        var tb = new Microsoft.UI.Xaml.Controls.TextBlock { Text=label, FontSize=9, Foreground=new SolidColorBrush(Theme.TextSec) };
        Canvas.SetLeft(tb, x + 10); Canvas.SetTop(tb, y);
        c.Children.Add(tb);
    }
}
