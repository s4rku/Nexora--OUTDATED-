using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NEXORA.Views;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace NEXORA;

public sealed partial class MainWindow : Window
{
    private Button? _activeBtn;

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr h);

    private static readonly Dictionary<string, Type> _pages = new()
    {
        ["Dashboard"]     = typeof(DashboardPage),
        ["PcOptimizer"]   = typeof(PcOptimizerPage),
        ["GameOptimizer"] = typeof(GameOptimizerPage),
        ["GameLibrary"]   = typeof(GameLibraryPage),
        ["Performance"]   = typeof(PerformancePage),
        ["Benchmark"]     = typeof(BenchmarkPage),
        ["Startup"]       = typeof(StartupManagerPage),
        ["Process"]       = typeof(ProcessManagerPage),
        ["Storage"]       = typeof(StorageCleanerPage),
        ["Network"]       = typeof(NetworkPage),
        ["Restore"]       = typeof(RestoreCenterPage),
        ["History"]       = typeof(OptimizationHistoryPage),
        ["AI"]            = typeof(AIAdvisorPage),
        ["Settings"]      = typeof(SettingsPage),
    };

    internal Button NavPcOptimizerBtn => NavPcOptimizer;

    public MainWindow()
    {
        InitializeComponent();
        Title = "NEXORA — Intelligent PC Performance";

        // Centre and size window
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        int sw = display.WorkArea.Width, sh = display.WorkArea.Height;
        int ww = Math.Min(1400, sw - 40), wh = Math.Min(880, sh - 60);
        AppWindow.MoveAndResize(new RectInt32((sw - ww) / 2, (sh - wh) / 2, ww, wh));

        // Force show
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ShowWindow(hwnd, 1);   // SW_SHOWNORMAL
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
        App.Log($"HWND={hwnd} Visible=true");

        ContentFrame.Loaded += (_, _) => Navigate("Dashboard", NavDashboard);
    }

    internal void Navigate(string tag, Button btn)
    {
        if (!_pages.TryGetValue(tag, out var pageType)) return;

        if (_activeBtn != null)
            _activeBtn.Style = (Style)Application.Current.Resources["NavBtnStyle"];

        btn.Style     = (Style)Application.Current.Resources["NavBtnActiveStyle"];
        _activeBtn    = btn;
        StatusText.Text = "Ready";

        App.Log($"Navigating to {tag} ({pageType.Name})");
        try
        {
            ContentFrame.Navigate(pageType);
            App.Log($"Navigation to {tag} succeeded.");
        }
        catch (Exception ex)
        {
            App.Log($"NAVIGATE CRASH [{tag}]: {ex}");
            StatusText.Text = $"Error — {ex.GetType().Name}";
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag?.ToString() is string tag)
            Navigate(tag, btn);
    }

    internal void SetStatus(string msg, bool isError = false)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StatusText.Text       = msg;
            StatusText.Foreground = isError
                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NxDangerBrush"]
                : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NxSuccessBrush"];
        });
    }
}
