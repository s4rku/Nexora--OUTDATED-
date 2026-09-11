using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NEXORA.Design;
using NEXORA.ViewModels;

namespace NEXORA.Views;

public sealed partial class OptimizationHistoryPage : Page
{
    private RestoreCenterViewModel? _vm;
    private StackPanel? _list;
    private TextBlock?  _statusTb;

    public OptimizationHistoryPage() => InitializeComponent();

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
        catch (Exception ex) { App.Log($"OptimizationHistoryPage crash: {ex}"); }
    }

    private void Build()
    {
        Root.Children.Clear();
        Root.Children.Add(NxSection.Header("Optimization History",
            "A timeline of all optimization sessions applied to your system."));

        _statusTb = Theme.Caption("Loading…", Theme.TextSec);
        Root.Children.Add(_statusTb);
        Root.Children.Add(Theme.Overline("TIMELINE"));
        _list = new StackPanel { Spacing = 4 };
        Root.Children.Add(_list);
    }

    private void Refresh()
    {
        if (_vm == null || _list == null) return;
        if (_statusTb != null) _statusTb.Text = _vm.StatusMessage;
        _list.Children.Clear();

        if (!_vm.Sessions.Any())
        {
            var empty = new StackPanel { Spacing = 6 };
            empty.Children.Add(Theme.H3("No history yet"));
            empty.Children.Add(Theme.Body("Sessions appear here after you run optimizations.", Theme.TextSec));
            empty.Children.Add(Theme.Caption("Navigate to PC Optimizer to apply your first optimization.", Theme.TextMuted));
            _list.Children.Add(NxCard.Standard(empty));
            return;
        }

        foreach (var session in _vm.Sessions)
        {
            var row = new Grid { ColumnSpacing = 14 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Date column
            var datePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            datePanel.Children.Add(Theme.Caption($"{session.AppliedAt:MMM d}", Theme.TextSec));
            datePanel.Children.Add(Theme.Caption($"{session.AppliedAt:HH:mm}", Theme.TextMuted));
            Grid.SetColumn(datePanel, 0); row.Children.Add(datePanel);

            // Dot column
            var dot = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 10, Height = 10,
                Fill  = Theme.B(session.IsRolledBack ? Theme.TextMuted : Theme.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            Grid.SetColumn(dot, 1); row.Children.Add(dot);

            // Content column
            var content = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(Theme.H3(session.ProfileName,
                session.IsRolledBack ? Theme.TextMuted : Theme.TextPri));

            var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            meta.Children.Add(Theme.Caption(
                $"{session.AppliedOptimizations.Count(o => o.WasSuccessful)} changes  ·  {session.Level}",
                Theme.TextSec));
            if (session.IsRolledBack)
                meta.Children.Add(NxStatus.Pill("Rolled Back", NxStatus.StatusLevel.Warning));
            content.Children.Add(meta);
            Grid.SetColumn(content, 2); row.Children.Add(content);

            _list.Children.Add(new Border
            {
                Padding = new Thickness(0, 8, 0, 8),
                Child   = row,
                BorderBrush     = Theme.B(Theme.Border),
                BorderThickness = new Thickness(0, 0, 0, 1)
            });
        }
    }
}
