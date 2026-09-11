using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NEXORA.Core.Models;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class GameLibraryPage : Page
{
    private GameLibraryViewModel? _vm;
    private StackPanel? _gamesPanel;
    private TextBlock?  _statusTb;
    private TextBox?    _searchBox;
    private string      _filter = string.Empty;

    public GameLibraryPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            _vm = App.Services.GetRequiredService<GameLibraryViewModel>();
            _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(RefreshReal);
            BuildUI();
            try { await _vm.ScanGamesAsync(); } catch { }
        }
        catch (Exception ex) { App.Log($"GameLibraryPage crash: {ex}"); }
    }

    private void BuildUI()
    {
        Root.Children.Clear();

        // Header
        var hdrGrid = new Grid { ColumnSpacing = 16 };
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hdrLeft = new StackPanel { Spacing = 2 };
        hdrLeft.Children.Add(Theme.H1("Game Library"));
        _statusTb = Theme.Caption("Scanning for installed games…", Theme.TextSec);
        hdrLeft.Children.Add(_statusTb);
        Grid.SetColumn(hdrLeft, 0);

        var scanBtn = new Button
        {
            Content = "SCAN GAMES",
            Style   = (Style)Application.Current.Resources["NxPrimaryBtn"],
            VerticalAlignment = VerticalAlignment.Center
        };
        scanBtn.Click += async (_, _) =>
        {
            try { scanBtn.IsEnabled = false; if (_vm != null) await _vm.ScanGamesAsync(); }
            finally { scanBtn.IsEnabled = true; }
        };
        Grid.SetColumn(scanBtn, 1);
        hdrGrid.Children.Add(hdrLeft); hdrGrid.Children.Add(scanBtn);
        Root.Children.Add(hdrGrid);

        // Search
        _searchBox = new TextBox
        {
            PlaceholderText = "Search games by name or store…",
            FontSize        = 13,
            Padding         = new Thickness(14, 10, 14, 10),
            CornerRadius    = new CornerRadius(10),
            Background      = Theme.B(Theme.Surface2),
            Foreground      = Theme.B(Theme.TextPri),
            BorderBrush     = Theme.B(Theme.Border),
            BorderThickness = new Thickness(1)
        };
        _searchBox.TextChanged += (_, _) => { _filter = _searchBox.Text ?? ""; RefreshReal(); };
        Root.Children.Add(_searchBox);

        Root.Children.Add(Theme.Overline("DETECTED GAMES"));
        _gamesPanel = new StackPanel { Spacing = 0 };
        Root.Children.Add(_gamesPanel);
    }

    private void RenderMock() { /* No mock — only real detected games */ }

    private void RefreshReal()
    {
        if (_vm == null) return;
        if (_statusTb != null) _statusTb.Text = _vm.StatusMessage;

        var source = _vm.FilteredGames.Count > 0 ? _vm.FilteredGames : _vm.Games;
        var filtered = string.IsNullOrEmpty(_filter)
            ? source.ToList()
            : source.Where(g => g.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)
                             || g.Store.ToString().Contains(_filter, StringComparison.OrdinalIgnoreCase)).ToList();

        RenderGameList(filtered.Select(g => (
            g.Name,
            g.Store.ToString(),
            "Detected",
            0, 0,
            "Ready"
        )).ToList());
    }

    private void RenderGameList(List<(string Name, string Store, string Profile, int Target, int Current, string Status)> games)
    {
        if (_gamesPanel == null) return;
        _gamesPanel.Children.Clear();

        if (!games.Any())
        {
            _gamesPanel.Children.Add(NxCard.Standard(
                NxEmpty.Build(string.Empty, "No games found",
                    "Try scanning again or check your game installation paths.",
                    "SCAN GAMES", async (_, _) => { try { if (_vm != null) await _vm.ScanGamesAsync(); } catch { } })));
            return;
        }

        // 2-column grid
        var outerGrid = new Grid { ColumnSpacing = 10 };
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int r = 0, c = 0;
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        foreach (var (name, store, profile, target, current, status) in games)
        {
            if (c == 2) { c = 0; r++; outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); }
            var card = BuildGameCard(name, store, profile, target, current, status);
            Grid.SetColumn(card, c); Grid.SetRow(card, r);
            outerGrid.Children.Add(card);
            c++;
        }
        _gamesPanel.Children.Add(outerGrid);
    }

    private Border BuildGameCard(string name, string store, string profile, int target, int current, string status)
    {
        var isOpt = status == "Optimized";
        var panel = new StackPanel { Spacing = 10 };

        // Colour bar across top
        panel.Children.Add(new Border
        {
            Height          = 3,
            CornerRadius    = new CornerRadius(2),
            Background      = Theme.AccentGrad(),
            Margin          = new Thickness(-20, -16, -20, 0)
        });

        // Name + store row
        var nameRow = new Grid();
        nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var nameTb = Theme.H3(name);
        nameTb.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
        Grid.SetColumn(nameTb, 0);
        var storeBadge = NxStatus.Pill(store, NxStatus.StatusLevel.Info);
        storeBadge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(storeBadge, 1);
        nameRow.Children.Add(nameTb); nameRow.Children.Add(storeBadge);
        panel.Children.Add(nameRow);

        // Profile
        panel.Children.Add(Theme.Caption(profile, Theme.TextSec));

        // FPS row (only if we have real numbers)
        if (target > 0)
        {
            var fpsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            fpsRow.Children.Add(FpsStat("Current", $"{current}", Theme.Accent));
            fpsRow.Children.Add(FpsStat("Target",  $"{target}",  Theme.TextSec));
            panel.Children.Add(fpsRow);

            int pct = target > 0 ? (int)((double)current / target * 100) : 0;
            panel.Children.Add(NxScoreBar.Build("FPS", Math.Min(pct, 100),
                pct >= 90 ? Theme.Success : pct >= 70 ? Theme.Warning : Theme.Danger));
        }

        // Optimize button
        var btn = new Button
        {
            Content = isOpt ? "OPTIMIZED" : "OPTIMIZE",
            Style   = (Style)Application.Current.Resources[isOpt ? "NxOutlineBtn" : "NxPrimaryBtn"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin  = new Thickness(0, 4, 0, 0)
        };
        var capName = name;
        btn.Click += async (_, _) =>
        {
            try { await NxModal.ShowComingSoon(XamlRoot, $"Optimize {capName}"); } catch { }
        };
        panel.Children.Add(btn);

        return NxCard.Standard(panel, new Thickness(4));
    }

    private static StackPanel FpsStat(string label, string val, Color color)
    {
        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(Theme.Caption(label));
        sp.Children.Add(Theme.H3(val, color));
        return sp;
    }
}
