using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class ProcessManagerPage : Page
{
    private ProcessManagerViewModel? _vm;
    private StackPanel? _processList;
    private TextBlock?  _statusLabel;

    public ProcessManagerPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            _vm = App.Services.GetRequiredService<ProcessManagerViewModel>();
            _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(RefreshList);
            BuildUI();
            _ = _vm.RefreshAsync();
        }
        catch (Exception ex) { App.Log($"ProcessManagerPage crash: {ex}"); }
    }

    private void BuildUI()
    {
        Root.Children.Clear();

        // Header
        Root.Children.Add(NxSection.Header("PROCESS MANAGER", "Live view of your top 50 processes by CPU usage."));

        // Action row
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var refreshBtn = new Button { Content = "⟳  REFRESH", Style = (Style)Application.Current.Resources["NxPrimaryBtn"] };
        refreshBtn.Click += (_, _) =>
        {
            try { _ = _vm?.RefreshAsync(); } catch { }
        };
        var killBtn = new Button { Content = "END PROCESS", Style = (Style)Application.Current.Resources["NxDangerBtn"] };
        killBtn.Click += async (_, _) =>
        {
            try { await NxModal.ShowComingSoon(XamlRoot, "End Process"); } catch { }
        };
        actionRow.Children.Add(refreshBtn);
        actionRow.Children.Add(killBtn);
        Root.Children.Add(actionRow);

        // Status
        _statusLabel = Theme.Caption("Loading processes…", Theme.TextSec);
        Root.Children.Add(_statusLabel);

        // Column header
        var colHdr = new Grid { ColumnSpacing = 8, Margin = new Thickness(20, 4, 20, 0) };
        colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void AddHdr(string t, int col)
        {
            var tb = Theme.Caption(t.ToUpperInvariant(), Theme.TextMuted);
            tb.CharacterSpacing = 60;
            Grid.SetColumn(tb, col);
            colHdr.Children.Add(tb);
        }
        AddHdr("Process", 0); AddHdr("CPU %", 1); AddHdr("RAM MB", 2); AddHdr("Publisher", 3);
        Root.Children.Add(colHdr);

        // List container
        _processList = new StackPanel { Spacing = 2 };
        Root.Children.Add(_processList);
    }

    private void RefreshList()
    {
        if (_vm == null || _processList == null) return;
        try
        {
            if (_statusLabel != null) _statusLabel.Text = _vm.StatusMessage;
            _processList.Children.Clear();

            foreach (var proc in _vm.Processes.Take(50))
            {
                var grid = new Grid { ColumnSpacing = 8 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var nameTb = Theme.Body(proc.Name);
                nameTb.VerticalAlignment = VerticalAlignment.Center;
                nameTb.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;

                var cpuColor = proc.CpuUsagePercent > 20 ? Theme.Warning : proc.CpuUsagePercent > 50 ? Theme.Danger : Theme.TextPri;
                var cpuTb = Theme.H3($"{proc.CpuUsagePercent:F1}%", proc.CpuUsagePercent > 20 ? Theme.Warning : Theme.TextSec);
                cpuTb.VerticalAlignment = VerticalAlignment.Center;

                var ramTb = Theme.H3($"{proc.MemoryMB:F0}", proc.MemoryMB > 500 ? Theme.Warning : Theme.TextSec);
                ramTb.VerticalAlignment = VerticalAlignment.Center;

                var pubTb = Theme.Caption(string.IsNullOrEmpty(proc.Publisher) ? "—" : proc.Publisher);
                pubTb.VerticalAlignment = VerticalAlignment.Center;
                pubTb.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;

                Grid.SetColumn(nameTb, 0); Grid.SetColumn(cpuTb, 1);
                Grid.SetColumn(ramTb, 2);  Grid.SetColumn(pubTb, 3);
                grid.Children.Add(nameTb); grid.Children.Add(cpuTb);
                grid.Children.Add(ramTb);  grid.Children.Add(pubTb);

                _processList.Children.Add(NxCard.Standard(grid, new Thickness(0, 0, 0, 2)));
            }
        }
        catch (Exception ex) { App.Log($"ProcessManagerPage RefreshList crash: {ex}"); }
    }
}
