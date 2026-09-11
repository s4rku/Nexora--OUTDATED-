using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NEXORA.Design;
using NEXORA.ViewModels;

namespace NEXORA.Views;

public sealed partial class SettingsPage : Page
{
    private SettingsViewModel? _vm;

    public SettingsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            _vm = App.Services.GetRequiredService<SettingsViewModel>();
            Build();
        }
        catch (Exception ex) { App.Log($"SettingsPage crash: {ex}"); }
    }

    private void Build()
    {
        if (_vm == null) return;
        Root.Children.Clear();

        Root.Children.Add(NxSection.Header("Settings",
            "Adjust NEXORA's behaviour, privacy, and appearance preferences."));

        // General
        Root.Children.Add(Theme.Overline("GENERAL"));
        AddToggle("Automatic Game Launch Optimisation",
            "Apply a game's profile automatically when NEXORA detects it launching.",
            _vm.AutoGameLaunch, v => _vm.AutoGameLaunch = v);
        AddToggle("Background Monitoring",
            "Continue monitoring CPU and GPU usage when NEXORA is minimised.",
            _vm.BackgroundMonitoring, v => _vm.BackgroundMonitoring = v);
        AddToggle("Show FPS Overlay",
            "Display a lightweight FPS counter during gaming sessions (requires restart).",
            _vm.ShowFpsOverlay, v => _vm.ShowFpsOverlay = v);

        // Privacy
        Root.Children.Add(Theme.Overline("PRIVACY"));
        AddToggle("Telemetry",
            "Send anonymous usage statistics to help improve NEXORA. No personal or system data is ever transmitted. Disabled by default.",
            _vm.TelemetryEnabled, v => _vm.TelemetryEnabled = v);

        // Storage
        Root.Children.Add(Theme.Overline("DATA"));
        var dataPanel = new StackPanel { Spacing = 8 };
        dataPanel.Children.Add(Theme.H3("Local Data Locations"));
        dataPanel.Children.Add(DataRow("Database",  $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\NEXORA\\nexora.db"));
        dataPanel.Children.Add(DataRow("Log file",  $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\NEXORA\\startup.log"));
        dataPanel.Children.Add(DataRow("Settings",  $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\NEXORA\\settings.json"));
        dataPanel.Children.Add(Theme.Caption("All data is stored on your machine. Nothing is uploaded.", Theme.TextMuted));
        Root.Children.Add(NxCard.Standard(dataPanel));

        // About
        Root.Children.Add(Theme.Overline("ABOUT"));
        var aboutPanel = new StackPanel { Spacing = 8 };
        aboutPanel.Children.Add(Theme.H2("NEXORA"));
        aboutPanel.Children.Add(Theme.Body("Intelligent PC Performance Optimizer"));
        aboutPanel.Children.Add(Theme.Caption($"Version {_vm.AppVersion}", Theme.TextMuted));
        aboutPanel.Children.Add(Theme.Caption("Windows 10 / 11  ·  x64", Theme.TextMuted));
        Root.Children.Add(NxCard.Standard(aboutPanel));
    }

    private void AddToggle(string label, string desc, bool value, Action<bool> onChange)
    {
        var row = new Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { Spacing = 3 };
        info.Children.Add(Theme.Body(label));
        info.Children.Add(Theme.Caption(desc, Theme.TextSec));
        Grid.SetColumn(info, 0);

        var tog = new ToggleSwitch { IsOn = value, OnContent = "", OffContent = "", VerticalAlignment = VerticalAlignment.Center };
        tog.Toggled += (_, _) => onChange(tog.IsOn);
        Grid.SetColumn(tog, 1);

        row.Children.Add(info); row.Children.Add(tog);
        Root.Children.Add(NxCard.Standard(row));
    }

    private static Grid DataRow(string label, string path)
    {
        var g = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 2, 0, 2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lTb = Theme.Caption(label, Theme.TextSec); lTb.VerticalAlignment = VerticalAlignment.Center;
        var pTb = Theme.Caption(path,  Theme.TextMuted);
        pTb.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
        pTb.MaxLines = 1;
        Grid.SetColumn(lTb, 0); Grid.SetColumn(pTb, 1);
        g.Children.Add(lTb); g.Children.Add(pTb);
        return g;
    }
}
