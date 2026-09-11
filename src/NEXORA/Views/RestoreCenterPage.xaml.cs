using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class RestoreCenterPage : Page
{
    private RestoreCenterViewModel? _vm;
    private StackPanel? _sessionList;
    private TextBlock?  _statusTb;

    public RestoreCenterPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            _vm = App.Services.GetRequiredService<RestoreCenterViewModel>();
            _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
            Build();
            _ = _vm.LoadAsync();
        }
        catch (Exception ex) { App.Log($"RestoreCenterPage crash: {ex}"); }
    }

    private void Build()
    {
        Root.Children.Clear();

        Root.Children.Add(NxSection.Header("Restore Center",
            "Every NEXORA optimization is logged here. Roll back any change at any time."));

        // Trust card
        var trustGrid = new Grid { ColumnSpacing = 16 };
        trustGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        trustGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var trustIcon = new Border
        {
            Width = 40, Height = 40,
            CornerRadius    = new CornerRadius(10),
            Background      = Theme.B(Color.FromArgb(0x26, 0x00, 0xE6, 0x76)),
            BorderBrush     = Theme.B(Color.FromArgb(0x55, 0x00, 0xE6, 0x76)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "100%", FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = Theme.B(Theme.Success),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(trustIcon, 0);

        var trustText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        trustText.Children.Add(Theme.H3("Safe Rollback Guaranteed"));
        trustText.Children.Add(Theme.Body("All optimizations are reversible. Your original system state is always preserved.", Theme.TextSec));
        Grid.SetColumn(trustText, 1);

        trustGrid.Children.Add(trustIcon);
        trustGrid.Children.Add(trustText);
        Root.Children.Add(NxCard.Accent(trustGrid));

        _statusTb = Theme.Caption("Loading optimization history…", Theme.TextSec);
        Root.Children.Add(_statusTb);

        Root.Children.Add(Theme.Overline("OPTIMIZATION SESSIONS"));
        _sessionList = new StackPanel { Spacing = 6 };
        Root.Children.Add(_sessionList);
    }

    private void Refresh()
    {
        if (_vm == null || _sessionList == null) return;
        if (_statusTb != null) _statusTb.Text = _vm.StatusMessage;

        _sessionList.Children.Clear();

        if (!_vm.Sessions.Any())
        {
            _sessionList.Children.Add(NxCard.Standard(new StackPanel { Spacing = 8, Children =
            {
                Theme.H3("No sessions yet"),
                Theme.Body("Optimization sessions appear here after you apply changes in PC Optimizer.", Theme.TextSec),
                Theme.Caption("A system restore point is created before every optimization run.", Theme.TextMuted)
            }}));
            return;
        }

        foreach (var session in _vm.Sessions)
        {
            var card = BuildSessionCard(session);
            _sessionList.Children.Add(card);
        }
    }

    private Border BuildSessionCard(NEXORA.Core.Models.OptimizationSession session)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left colour bar
        var barColor = session.IsRolledBack ? Theme.TextMuted : Theme.Accent;
        row.Children.Add(new Border
        {
            Width = 4, CornerRadius = new CornerRadius(2),
            Background = Theme.B(barColor),
            VerticalAlignment = VerticalAlignment.Stretch
        });

        // Info
        var info = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(Theme.H3(session.ProfileName));

        var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        metaRow.Children.Add(Theme.Caption($"{session.AppliedAt:ddd, MMM d  HH:mm}", Theme.TextMuted));
        metaRow.Children.Add(Theme.Caption($"·  {session.AppliedOptimizations.Count(o => o.WasSuccessful)} changes", Theme.TextSec));
        if (session.IsRolledBack)
            metaRow.Children.Add(NxStatus.Pill("ROLLED BACK", NxStatus.StatusLevel.Warning));
        info.Children.Add(metaRow);

        // Show individual changes summary
        var categories = session.AppliedOptimizations
            .Where(o => o.WasSuccessful)
            .GroupBy(o => o.Category)
            .Select(g => g.Key)
            .Take(4);
        if (categories.Any())
        {
            var tagRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };
            foreach (var cat in categories)
                tagRow.Children.Add(NxStatus.Pill(cat, NxStatus.StatusLevel.Info));
            info.Children.Add(tagRow);
        }
        Grid.SetColumn(info, 1); row.Children.Add(info);

        // View button
        var viewBtn = new Button
        {
            Content  = "VIEW",
            Style    = (Style)Application.Current.Resources["NxOutlineBtn"],
            Padding  = new Thickness(14, 6, 14, 6),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        viewBtn.Click += async (_, _) =>
        {
            try
            {
                var changes = string.Join("\n", session.AppliedOptimizations
                    .Take(10)
                    .Select(o => $"  {(o.WasSuccessful ? "+" : "x")}  {o.Name}"));
                await NxModal.Show(XamlRoot,
                    $"Session: {session.ProfileName}",
                    changes.Length > 0 ? changes : "No changes recorded.",
                    "Close");
            }
            catch { }
        };
        Grid.SetColumn(viewBtn, 2); row.Children.Add(viewBtn);

        // Restore button
        var restoreBtn = new Button
        {
            Content   = "RESTORE",
            IsEnabled = !session.IsRolledBack,
            Style     = (Style)Application.Current.Resources["NxDangerBtn"],
            Padding   = new Thickness(14, 6, 14, 6),
            FontSize  = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        var cap = session;
        restoreBtn.Click += async (_, _) =>
        {
            try
            {
                var ok = await NxModal.Show(XamlRoot,
                    "Confirm Restore",
                    $"This will reverse all {cap.AppliedOptimizations.Count(o => o.WasSuccessful)} changes from session \"{cap.ProfileName}\".\n\nContinue?",
                    "RESTORE", "Cancel");
                if (ok && _vm != null)
                {
                    restoreBtn.IsEnabled = false;
                    await _vm.RollbackSessionAsync(cap);
                }
            }
            catch { restoreBtn.IsEnabled = !cap.IsRolledBack; }
        };
        Grid.SetColumn(restoreBtn, 3); row.Children.Add(restoreBtn);

        return NxCard.Standard(row);
    }
}
