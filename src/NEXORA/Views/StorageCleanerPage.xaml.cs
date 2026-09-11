using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class StorageCleanerPage : Page
{
    private StorageCleanerViewModel? _vm;
    private StackPanel? _categoryList;
    private TextBlock?  _statusTb, _totalTb;
    private Button?     _cleanBtn;

    public StorageCleanerPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            _vm = App.Services.GetRequiredService<StorageCleanerViewModel>();
            _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(RefreshScan);
            Build();
        }
        catch (Exception ex) { App.Log($"StorageCleanerPage crash: {ex}"); }
    }

    private void Build()
    {
        Root.Children.Clear();

        Root.Children.Add(NxSection.Header("Storage Cleaner",
            "Safely reclaim disk space by removing temporary files, caches, and junk."));

        // Drive summary from real hardware data
        var hw = MockHardwareData.Instance;
        if (hw.StorageTotalGB > 0)
        {
            var drivePanel = new StackPanel { Spacing = 8 };
            drivePanel.Children.Add(Theme.Overline("SYSTEM DRIVE"));
            var driveRow = new Grid { ColumnSpacing = 20 };
            driveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            driveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            driveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            driveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            var totalS = DriveStat("Total", $"{hw.StorageTotalGB:F0} GB", Theme.TextPri);
            Grid.SetColumn(totalS, 0); driveRow.Children.Add(totalS);
            var freeS = DriveStat("Free",  $"{hw.StorageFreeGB:F0} GB",                    Theme.Success);
            Grid.SetColumn(freeS,  1); driveRow.Children.Add(freeS);
            var usedS = DriveStat("Used",  $"{hw.StorageTotalGB - hw.StorageFreeGB:F0} GB", Theme.Warning);
            Grid.SetColumn(usedS,  2); driveRow.Children.Add(usedS);

            // Usage bar
            var barBg   = new Border { Height=6, CornerRadius=new CornerRadius(3), Background=Theme.B(Theme.Border) };
            var barFill = new Border { Height=6, CornerRadius=new CornerRadius(3), Background=Theme.AccentGrad(), HorizontalAlignment=HorizontalAlignment.Left, Width=0 };
            var barGrid = new Grid();
            barGrid.Children.Add(barBg); barGrid.Children.Add(barFill);
            var pct = hw.StorageUsagePct; var cf = barFill;
            barGrid.SizeChanged += (_, _) => cf.Width = Math.Max(0, barGrid.ActualWidth * pct / 100.0);
            var barSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; barSp.Children.Add(barGrid);
            Grid.SetColumn(barSp, 3); driveRow.Children.Add(barSp);

            drivePanel.Children.Add(driveRow);
            Root.Children.Add(NxCard.Standard(drivePanel));
        }

        // Action buttons
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var scanBtn = new Button { Content = "SCAN STORAGE", Style = (Style)Application.Current.Resources["NxOutlineBtn"] };
        scanBtn.Click += async (_, _) =>
        {
            try { scanBtn.IsEnabled = false; if (_vm != null) await _vm.ScanAsync(); }
            finally { scanBtn.IsEnabled = true; }
        };
        _cleanBtn = new Button { Content = "CLEAN SELECTED", Style = (Style)Application.Current.Resources["NxPrimaryBtn"], IsEnabled = false };
        _cleanBtn.Click += async (_, _) =>
        {
            try
            {
                if (_cleanBtn != null) _cleanBtn.IsEnabled = false;
                if (_vm != null) await _vm.CleanAsync();
            }
            catch { if (_cleanBtn != null) _cleanBtn.IsEnabled = true; }
        };
        btnRow.Children.Add(scanBtn);
        btnRow.Children.Add(_cleanBtn);
        Root.Children.Add(btnRow);

        _statusTb = Theme.Caption("Click SCAN STORAGE to analyse your drives.", Theme.TextSec);
        Root.Children.Add(_statusTb);

        _totalTb = new TextBlock { FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable Display"), FontSize = 28, Foreground = Theme.B(Theme.Success) };
        Root.Children.Add(_totalTb);

        Root.Children.Add(Theme.Overline("SCAN RESULTS"));
        _categoryList = new StackPanel { Spacing = 6 };
        Root.Children.Add(_categoryList);
    }

    private void RefreshScan()
    {
        if (_vm == null || _categoryList == null) return;
        if (_statusTb != null) _statusTb.Text = _vm.StatusMessage;
        if (_totalTb  != null) _totalTb.Text  = _vm.Categories.Any() ? $"Reclaimable: {_vm.TotalReclaimable}" : "";
        if (_cleanBtn != null) _cleanBtn.IsEnabled = _vm.Categories.Any(c => c.IsSelected);

        _categoryList.Children.Clear();
        if (!_vm.Categories.Any()) return;

        foreach (var cat in _vm.Categories)
        {
            var catColor = cat.Risk == NEXORA.Core.Models.RiskLevel.Safe   ? Theme.Success :
                           cat.Risk == NEXORA.Core.Models.RiskLevel.Low    ? Theme.Warning :
                           Theme.Danger;

            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Colour indicator bar
            row.Children.Add(new Border { Width=4, CornerRadius=new CornerRadius(2), Background=Theme.B(catColor), VerticalAlignment=VerticalAlignment.Stretch });

            var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(Theme.H3(cat.Name));
            info.Children.Add(Theme.Caption(cat.Description));
            Grid.SetColumn(info, 1); row.Children.Add(info);

            var sizeTb = Theme.H3(cat.ReclaimableMB > 1024 ? $"{cat.ReclaimableGB:F2} GB" : $"{cat.ReclaimableMB:F0} MB", Theme.Accent);
            sizeTb.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(sizeTb, 2); row.Children.Add(sizeTb);

            var cb = new CheckBox { IsChecked = cat.IsSelected, VerticalAlignment = VerticalAlignment.Center };
            cb.Checked   += (_, _) => { cat.IsSelected = true;  if (_cleanBtn != null) _cleanBtn.IsEnabled = true; };
            cb.Unchecked += (_, _) => cat.IsSelected = false;
            Grid.SetColumn(cb, 3); row.Children.Add(cb);

            _categoryList.Children.Add(NxCard.Standard(row));
        }
    }

    private static StackPanel DriveStat(string label, string val, Color color)
    {
        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(Theme.Caption(label));
        sp.Children.Add(Theme.H3(val, color));
        return sp;
    }
}
