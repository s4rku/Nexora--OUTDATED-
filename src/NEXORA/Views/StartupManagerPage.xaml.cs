using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NEXORA.Core.Models;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class StartupManagerPage : Page
{
    private StartupManagerViewModel? _vm;
    private StackPanel? _list;
    private TextBlock?  _statusTb;
    private TextBlock?  _countTb;

    public StartupManagerPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            _vm = App.Services.GetRequiredService<StartupManagerViewModel>();
            _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
            BuildUI();
            _ = _vm.LoadAsync();
        }
        catch (Exception ex) { App.Log($"StartupManagerPage crash: {ex}"); }
    }

    private void BuildUI()
    {
        Root.Children.Clear();

        // Header
        Root.Children.Add(NxSection.Header("Startup Manager", "Control which applications start automatically with Windows."));

        // Summary row
        Root.Children.Add(NxCard.Accent(new Grid()));  // placeholder — rebuilt in Refresh

        // Action buttons
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var refreshBtn = new Button { Content = "REFRESH", Style = (Style)Application.Current.Resources["NxOutlineBtn"] };
        refreshBtn.Click += async (_, _) => { try { if (_vm != null) await _vm.LoadAsync(); } catch { } };
        btnRow.Children.Add(refreshBtn);
        Root.Children.Add(btnRow);

        _statusTb = Theme.Caption("Loading startup entries from registry…", Theme.TextSec);
        Root.Children.Add(_statusTb);

        Root.Children.Add(Theme.Overline("STARTUP ENTRIES"));
        _list = new StackPanel { Spacing = 4 };
        Root.Children.Add(_list);
    }

    private void Refresh()
    {
        if (_vm == null || _list == null) return;
        if (_statusTb != null) _statusTb.Text = _vm.StatusMessage;

        _list.Children.Clear();

        if (!_vm.Entries.Any())
        {
            _list.Children.Add(NxCard.Standard(NxEmpty.Build(
                string.Empty, "No startup entries found",
                "Try refreshing or check that the app is running with sufficient permissions.",
                "REFRESH", async (_, _) => { try { if (_vm != null) await _vm.LoadAsync(); } catch { } })));
            return;
        }

        foreach (var entry in _vm.Entries)
            _list.Children.Add(BuildEntryCard(entry));
    }

    private Border BuildEntryCard(StartupEntry entry)
    {
        var impactColor = entry.Impact switch
        {
            StartupImpact.High     => Theme.Danger,
            StartupImpact.Critical => Color.FromArgb(0xFF, 0xFF, 0x00, 0x55),
            StartupImpact.Medium   => Theme.Warning,
            _                      => Theme.Success
        };

        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

        // Info
        var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

        var nameTb = Theme.H3(entry.Name);
        if (entry.IsCriticalSystem)
        {
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            nameRow.Children.Add(nameTb);
            nameRow.Children.Add(NxStatus.Pill("SYSTEM", NxStatus.StatusLevel.Info));
            info.Children.Add(nameRow);
        }
        else info.Children.Add(nameTb);

        var pubRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        pubRow.Children.Add(Theme.Caption(string.IsNullOrEmpty(entry.Publisher) ? "Unknown publisher" : entry.Publisher, Theme.TextMuted));
        pubRow.Children.Add(Theme.Caption(entry.Location.ToString(), Theme.TextMuted));
        info.Children.Add(pubRow);
        Grid.SetColumn(info, 0);

        // Impact badge
        var badge = NxStatus.Pill(entry.Impact.ToString(),
            entry.Impact == StartupImpact.Low ? NxStatus.StatusLevel.Good :
            entry.Impact == StartupImpact.Medium ? NxStatus.StatusLevel.Warning :
            NxStatus.StatusLevel.Danger);
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(badge, 1);

        // Toggle + disable button
        var ctrl = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };

        var tog = new ToggleSwitch { IsOn = entry.IsEnabled, IsEnabled = !entry.IsCriticalSystem, OnContent = "", OffContent = "" };
        tog.Toggled += async (_, _) =>
        {
            try
            {
                tog.IsEnabled = false;
                if (_vm != null) await _vm.ToggleEntryAsync(entry);
                tog.IsEnabled = !entry.IsCriticalSystem;
            }
            catch { tog.IsEnabled = !entry.IsCriticalSystem; }
        };

        var actionBtn = new Button
        {
            Content  = entry.IsEnabled ? "Disable" : "Enable",
            Style    = (Style)Application.Current.Resources[entry.IsEnabled ? "NxDangerBtn" : "NxOutlineBtn"],
            Padding  = new Thickness(10, 5, 10, 5),
            FontSize = 11,
            IsEnabled = !entry.IsCriticalSystem
        };
        actionBtn.Click += async (_, _) =>
        {
            try
            {
                actionBtn.IsEnabled = false;
                if (_vm != null) await _vm.ToggleEntryAsync(entry);
            }
            catch { actionBtn.IsEnabled = !entry.IsCriticalSystem; }
        };

        ctrl.Children.Add(tog);
        ctrl.Children.Add(actionBtn);
        Grid.SetColumn(ctrl, 2);

        row.Children.Add(info); row.Children.Add(badge); row.Children.Add(ctrl);
        return NxCard.Standard(row);
    }
}
