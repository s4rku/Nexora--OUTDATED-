using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NEXORA.Core.Models;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class PcOptimizerPage : Page
{
    private PcOptimizerViewModel _vm = null!;

    public PcOptimizerPage()
    {
        InitializeComponent();
        try
        {
            _vm = App.Services.GetRequiredService<PcOptimizerViewModel>();
            _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateUI);
            Loaded += (_, _) => LevelBalanced.IsChecked = true;
        }
        catch (Exception ex) { App.Log($"PcOptimizerPage ctor crash: {ex}"); }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try { UpdateUI(); } catch (Exception ex) { App.Log($"PcOptimizerPage OnNavigatedTo crash: {ex}"); }
    }

    private void UpdateUI()
    {
        try
        {
            StatusText.Text = _vm.StatusMessage;
            ProgressBar.Value = _vm.ProgressValue;
            ProgressBar.Visibility = _vm.IsOptimizing ? Visibility.Visible : Visibility.Collapsed;

            ScanBtn.IsEnabled  = !_vm.IsAnalyzing && !_vm.IsOptimizing;
            ApplyBtn.IsEnabled = _vm.Optimizations.Any(o => o.IsSelected) && !_vm.IsOptimizing;
            RollbackBtn.IsEnabled = _vm.LastSession != null;

            FooterText.Text = $"{_vm.Optimizations.Count(o => o.IsSelected)} of {_vm.Optimizations.Count} selected";

            OptimizationList.Children.Clear();
            var byCategory = _vm.Optimizations.GroupBy(o => o.Category);
            foreach (var group in byCategory)
            {
                OptimizationList.Children.Add(Theme.Overline(group.Key));
                foreach (var opt in group)
                    OptimizationList.Children.Add(BuildOptimizationCard(opt));
            }
        }
        catch (Exception ex) { App.Log($"PcOptimizerPage UpdateUI crash: {ex}"); }
    }

    private Border BuildOptimizationCard(OptimizationDescriptor opt)
    {
        try
        {
            var riskColor = opt.Risk switch
            {
                RiskLevel.Safe   => Theme.Success,
                RiskLevel.Low    => Theme.Info,
                RiskLevel.Medium => Theme.Warning,
                RiskLevel.High   => Theme.Danger,
                _                => Theme.TextSec
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: info
            var info = new StackPanel { Spacing = 4 };

            var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            nameRow.Children.Add(Theme.H3(opt.Name));
            nameRow.Children.Add(NxStatus.Pill(opt.Risk.ToString(), opt.Risk switch
            {
                RiskLevel.Safe   => NxStatus.StatusLevel.Good,
                RiskLevel.Low    => NxStatus.StatusLevel.Info,
                RiskLevel.Medium => NxStatus.StatusLevel.Warning,
                _                => NxStatus.StatusLevel.Danger
            }));
            info.Children.Add(nameRow);
            info.Children.Add(Theme.Body(opt.Description, Theme.TextSec));

            if (!string.IsNullOrEmpty(opt.ExpectedBenefit))
                info.Children.Add(Theme.Caption($"Expected: {opt.ExpectedBenefit}", Theme.Accent));
            if (!string.IsNullOrEmpty(opt.CurrentValue))
                info.Children.Add(Theme.Caption($"Current: {opt.CurrentValue}", Theme.TextMuted));

            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            var cb = new CheckBox
            {
                IsChecked         = opt.IsSelected,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(8, 0, 0, 0)
            };
            cb.Checked   += (_, _) => { opt.IsSelected = true;  UpdateFooter(); };
            cb.Unchecked += (_, _) => { opt.IsSelected = false; UpdateFooter(); };
            Grid.SetColumn(cb, 1);
            grid.Children.Add(cb);

            return NxCard.Standard(grid, new Thickness(0, 0, 0, 4));
        }
        catch
        {
            return NxCard.Standard(Theme.Caption(opt.Name), new Thickness(0, 0, 0, 4));
        }
    }

    private void UpdateFooter()
    {
        try
        {
            ApplyBtn.IsEnabled = _vm.Optimizations.Any(o => o.IsSelected);
            FooterText.Text    = $"{_vm.Optimizations.Count(o => o.IsSelected)} of {_vm.Optimizations.Count} selected";
        }
        catch { }
    }

    private void Level_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is RadioButton rb && Enum.TryParse<OptimizationLevel>(rb.Tag?.ToString(), out var level))
                _vm.SelectedLevel = level;
        }
        catch { }
    }

    private void ScanBtn_Click(object sender, RoutedEventArgs e)      { try { _ = _vm.AnalyzeAsync(); } catch { } }
    private void ApplyBtn_Click(object sender, RoutedEventArgs e)     { try { _ = _vm.ApplySelectedAsync(); } catch { } }
    private void SelectAllBtn_Click(object sender, RoutedEventArgs e) { try { _vm.ToggleAll(true); UpdateUI(); } catch { } }
    private void RollbackBtn_Click(object sender, RoutedEventArgs e)  { try { _ = _vm.RollbackLastSessionAsync(); } catch { } }
}
