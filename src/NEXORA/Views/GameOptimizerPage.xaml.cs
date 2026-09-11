using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NEXORA.Core.Models;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class GameOptimizerPage : Page
{
    private GameOptimizerViewModel? _vm;
    private StackPanel? _gameGrid;
    private StackPanel? _detailPanel;
    private TextBlock?  _statusTb;
    private TextBlock?  _countTb;
    private bool _scanned;

    public GameOptimizerPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            _vm = App.Services.GetRequiredService<GameOptimizerViewModel>();
            _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(RefreshGameList);
            BuildUI();

            // Scan once per session
            if (!_scanned)
            {
                _scanned = true;
                try { await _vm.LoadAsync(); }
                catch (Exception ex) { App.Log($"GameOptimizerPage scan failed: {ex.Message}"); }
            }
            else
            {
                RefreshGameList();
            }
        }
        catch (Exception ex) { App.Log($"GameOptimizerPage crash: {ex}"); }
    }

    private void BuildUI()
    {
        Root.Children.Clear();

        // Header
        Root.Children.Add(NxSection.Header("Game Optimizer",
            "Automatically detects installed games and creates hardware-tuned optimization profiles."));

        // Hardware strip — shows your actual detected hardware
        var hw = MockHardwareData.Instance;
        var hwPanel = new StackPanel { Spacing = 8 };
        hwPanel.Children.Add(Theme.Overline("YOUR HARDWARE"));
        var hwRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        hwRow.Children.Add(HwStat("CPU",     string.IsNullOrEmpty(hw.CpuName) || hw.CpuName == "Scanning…" ? "Scanning…" : hw.CpuName, Theme.Accent));
        hwRow.Children.Add(HwStat("GPU",     string.IsNullOrEmpty(hw.GpuName) || hw.GpuName == "Scanning…" ? "Scanning…" : hw.GpuName, Theme.AccentPurp));
        hwRow.Children.Add(HwStat("RAM",     hw.RamTotalGB > 0 ? $"{hw.RamTotalGB:F0} GB" : "—", Theme.Success));
        hwRow.Children.Add(HwStat("VRAM",    hw.GpuVramGB > 0  ? $"{hw.GpuVramGB:F0} GB"  : "—", Theme.Warning));
        hwRow.Children.Add(HwStat("Display", !string.IsNullOrEmpty(hw.DisplayRes) && hw.DisplayRes != "—" ? hw.DisplayRes : "—", Theme.Info));
        hwPanel.Children.Add(hwRow);
        Root.Children.Add(NxCard.Accent(hwPanel));

        // Scan button + status
        var actionRow = new Grid { ColumnSpacing = 12 };
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _statusTb = Theme.Caption("Scanning for installed games…", Theme.TextSec);
        _statusTb.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_statusTb, 0);

        var scanBtn = new Button
        {
            Content = "RESCAN GAMES",
            Style   = (Style)Application.Current.Resources["NxOutlineBtn"],
            VerticalAlignment = VerticalAlignment.Center
        };
        scanBtn.Click += async (_, _) =>
        {
            try
            {
                scanBtn.IsEnabled = false;
                _scanned = false;
                if (_vm != null) await _vm.LoadAsync();
            }
            finally { scanBtn.IsEnabled = true; }
        };
        Grid.SetColumn(scanBtn, 1);
        actionRow.Children.Add(_statusTb); actionRow.Children.Add(scanBtn);
        Root.Children.Add(actionRow);

        // Games section header
        var gamesHdr = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        gamesHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gamesHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var ghdrTb = Theme.Overline("DETECTED GAMES");
        Grid.SetColumn(ghdrTb, 0);
        _countTb = Theme.Caption("", Theme.TextMuted);
        _countTb.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_countTb, 1);
        gamesHdr.Children.Add(ghdrTb); gamesHdr.Children.Add(_countTb);
        Root.Children.Add(gamesHdr);

        // Game grid
        _gameGrid = new StackPanel { Spacing = 8 };
        Root.Children.Add(_gameGrid);

        // Detail panel
        _detailPanel = new StackPanel { Spacing = 12, Visibility = Visibility.Collapsed };
        Root.Children.Add(_detailPanel);
    }

    private void RefreshGameList()
    {
        if (_vm == null || _gameGrid == null) return;

        if (_statusTb != null)
            _statusTb.Text = _vm.IsAnalyzing ? "Scanning…" : _vm.StatusMessage;

        var games = _vm.Games.ToList();
        if (_countTb != null)
            _countTb.Text = games.Count > 0 ? $"{games.Count} game{(games.Count == 1 ? "" : "s")}" : "";

        _gameGrid.Children.Clear();

        if (!games.Any())
        {
            if (!_vm.IsAnalyzing)
            {
                var empty = new StackPanel { Spacing = 10, Margin = new Thickness(0, 24, 0, 24) };
                empty.Children.Add(Theme.H3("No games found", Theme.TextMuted));
                empty.Children.Add(Theme.Body("NEXORA scanned Steam, Epic, Riot, Rockstar, and the Windows registry.\nMake sure your games are installed and try rescanning.", Theme.TextSec));
                empty.Children.Add(Theme.Caption("Supported stores: Steam · Epic Games · Riot Games · Rockstar · GOG · FiveM · Roblox", Theme.TextMuted));
                _gameGrid.Children.Add(NxCard.Standard(empty));
            }
            else
            {
                // Loading state
                for (int i = 0; i < 3; i++)
                    _gameGrid.Children.Add(BuildSkeletonCard());
            }
            return;
        }

        // 2-column grid
        var outerGrid = new Grid { ColumnSpacing = 10 };
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int r = 0, c = 0;
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        foreach (var game in games)
        {
            if (c == 2) { c = 0; r++; outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); }
            var card = BuildGameCard(game);
            Grid.SetColumn(card, c); Grid.SetRow(card, r);
            outerGrid.Children.Add(card);
            c++;
        }
        _gameGrid.Children.Add(outerGrid);
    }

    private Border BuildGameCard(GameEntry game)
    {
        var panel = new StackPanel { Spacing = 10 };

        // Accent top bar with store colour
        var storeColor = game.Store switch
        {
            GameStore.Steam        => Theme.Accent,
            GameStore.EpicGames    => Color.FromArgb(0xFF, 0x2A, 0x7F, 0xFF),
            GameStore.RiotGames    => Color.FromArgb(0xFF, 0xFF, 0x44, 0x44),
            GameStore.GOG          => Color.FromArgb(0xFF, 0xA9, 0x5C, 0xFF),
            _                      => Theme.TextSec
        };

        panel.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Height   = 3,
            RadiusX  = 2,
            RadiusY  = 2,
            Fill     = Theme.B(storeColor),
            Margin   = new Thickness(-20, -16, -20, 0)
        });

        // Name + store badge
        var nameRow = new Grid { ColumnSpacing = 8 };
        nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameTb = Theme.H3(game.Name);
        nameTb.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
        nameTb.MaxLines = 1;
        Grid.SetColumn(nameTb, 0);

        var storeBadge = NxStatus.Pill(game.Store.ToString(), NxStatus.StatusLevel.Info);
        storeBadge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(storeBadge, 1);
        nameRow.Children.Add(nameTb); nameRow.Children.Add(storeBadge);
        panel.Children.Add(nameRow);

        // Install path (abbreviated)
        var pathTb = Theme.Caption(AbbreviatePath(game.InstallDirectory), Theme.TextMuted);
        pathTb.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
        pathTb.MaxLines = 1;
        panel.Children.Add(pathTb);

        // Size
        if (game.InstallSizeBytes > 0)
            panel.Children.Add(Theme.Caption($"{game.InstallSizeBytes / 1_073_741_824.0:F1} GB installed", Theme.TextSec));

        // Profile + hardware analysis based on real hardware
        var hw     = MockHardwareData.Instance;
        var (profile, targetFps) = RecommendProfile(game, hw);
        var profRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        profRow.Children.Add(NxStatus.Pill(profile, NxStatus.StatusLevel.Info));
        if (targetFps > 0)
            profRow.Children.Add(Theme.Caption($"Target: {targetFps} FPS", Theme.TextSec));
        panel.Children.Add(profRow);

        // Buttons
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };

        var optBtn = new Button
        {
            Content = "OPTIMIZE",
            Style   = (Style)Application.Current.Resources["NxPrimaryBtn"],
            Padding = new Thickness(16, 8, 16, 8)
        };
        var capGame = game;
        optBtn.Click += async (_, _) =>
        {
            try { await NxModal.ShowComingSoon(XamlRoot, $"Optimize {capGame.Name}"); } catch { }
        };

        var detailBtn = new Button
        {
            Content  = "Details",
            Style    = (Style)Application.Current.Resources["NxOutlineBtn"],
            Padding  = new Thickness(14, 8, 14, 8),
            FontSize = 12
        };
        detailBtn.Click += (_, _) => ShowDetail(capGame, profile, targetFps);

        btnRow.Children.Add(optBtn); btnRow.Children.Add(detailBtn);
        panel.Children.Add(btnRow);

        return NxCard.Standard(panel, new Thickness(4));
    }

    private void ShowDetail(GameEntry game, string profile, int targetFps)
    {
        if (_detailPanel == null) return;
        _detailPanel.Visibility = Visibility.Visible;
        _detailPanel.Children.Clear();

        _detailPanel.Children.Add(Theme.Overline($"PROFILE: {game.Name.ToUpperInvariant()}"));

        var hw = MockHardwareData.Instance;

        // Hardware impact
        var (cpuImpact, gpuImpact, ramImpact) = EstimateImpact(game);
        var impPanel = new StackPanel { Spacing = 8 };
        impPanel.Children.Add(Theme.H3("Hardware Impact Estimate"));
        impPanel.Children.Add(Theme.Caption("Based on the game engine and typical workload for your hardware class.", Theme.TextSec));
        impPanel.Children.Add(NxScoreBar.Build("CPU Impact", cpuImpact, Theme.Accent));
        impPanel.Children.Add(NxScoreBar.Build("GPU Impact", gpuImpact, Theme.AccentPurp));
        impPanel.Children.Add(NxScoreBar.Build("RAM Impact", ramImpact, Theme.Success));
        _detailPanel.Children.Add(NxCard.Standard(impPanel));

        // Settings
        var recPanel = new StackPanel { Spacing = 0 };
        recPanel.Children.Add(Theme.H3("Recommended Settings"));
        recPanel.Children.Add(Theme.Caption($"Tuned for your {hw.GpuName}, {hw.RamTotalGB:F0} GB RAM.", Theme.TextSec));
        recPanel.Children.Add(Theme.HSep(0.3));
        recPanel.Children.Add(SettingRow("Profile",          profile,                "Selected based on your hardware"));
        recPanel.Children.Add(SettingRow("Frame Target",     targetFps > 0 ? $"{targetFps} FPS" : "Unlimited", "Match your monitor refresh rate"));
        recPanel.Children.Add(SettingRow("VSync",            "Off",                  "Reduces input latency"));
        recPanel.Children.Add(SettingRow("Resolution Scale", hw.GpuVramGB >= 4 ? "100%" : "85%", hw.GpuVramGB >= 4 ? "VRAM sufficient" : "Low VRAM — scale down"));
        recPanel.Children.Add(SettingRow("Texture Quality",  hw.GpuVramGB >= 4 ? "High" : "Medium", hw.GpuVramGB >= 4 ? "GPU VRAM sufficient" : "Reduce for VRAM stability"));
        recPanel.Children.Add(SettingRow("Shadows",          "Medium",               "Best performance/quality ratio"));
        recPanel.Children.Add(SettingRow("Anti-Aliasing",    "TAA",                  "Low performance cost, good quality"));
        _detailPanel.Children.Add(NxCard.Standard(recPanel));

        // Action buttons
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var applyBtn = new Button { Content = "APPLY PROFILE", Style = (Style)Application.Current.Resources["NxPrimaryBtn"] };
        applyBtn.Click += async (_, _) => { try { await NxModal.ShowComingSoon(XamlRoot, "Apply Profile"); } catch { } };
        var restoreBtn = new Button { Content = "RESTORE DEFAULTS", Style = (Style)Application.Current.Resources["NxDangerBtn"] };
        restoreBtn.Click += async (_, _) => { try { await NxModal.ShowComingSoon(XamlRoot, "Restore Defaults"); } catch { } };
        actionRow.Children.Add(applyBtn); actionRow.Children.Add(restoreBtn);
        _detailPanel.Children.Add(actionRow);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (string profile, int targetFps) RecommendProfile(GameEntry game, MockHardwareData hw)
    {
        // Base FPS target on monitor refresh rate
        int fps = 60;

        // Use competitive profile for known competitive games
        var nameLower = game.Name.ToLowerInvariant();
        bool isCompetitive = nameLower.Contains("valorant") || nameLower.Contains("cs") ||
                             nameLower.Contains("counter-strike") || nameLower.Contains("league") ||
                             nameLower.Contains("apex") || nameLower.Contains("overwatch") ||
                             nameLower.Contains("rocket league");

        bool isHeavy = nameLower.Contains("gta") || nameLower.Contains("red dead") ||
                       nameLower.Contains("cyberpunk") || nameLower.Contains("witcher") ||
                       nameLower.Contains("elden ring");

        // GPU-tier based recommendation
        var vram = hw.GpuVramGB;
        var profile = vram >= 8 ? (isCompetitive ? "Competitive" : isHeavy ? "Balanced" : "Quality")
                    : vram >= 4 ? (isCompetitive ? "Competitive" : "Balanced")
                    : "Performance"; // Low VRAM — prioritise FPS

        fps = profile switch
        {
            "Competitive" => 144,
            "Quality"     => 60,
            "Performance" => 60,
            _             => 75
        };

        return (profile, fps);
    }

    private static (int cpu, int gpu, int ram) EstimateImpact(GameEntry game)
    {
        var name = game.Name.ToLowerInvariant();

        // Known game workload profiles
        if (name.Contains("gta") || name.Contains("grand theft"))    return (80, 90, 65);
        if (name.Contains("fivem"))                                   return (85, 75, 60);
        if (name.Contains("valorant"))                                return (55, 65, 40);
        if (name.Contains("roblox"))                                  return (60, 35, 50);
        if (name.Contains("minecraft"))                               return (75, 40, 70);
        if (name.Contains("fortnite"))                                return (65, 80, 55);
        if (name.Contains("cs") || name.Contains("counter"))         return (65, 75, 40);
        if (name.Contains("league") || name.Contains("lol"))         return (50, 50, 40);
        if (name.Contains("apex"))                                    return (70, 85, 55);
        if (name.Contains("red dead") || name.Contains("rdr"))       return (75, 95, 70);

        // Default estimate
        return (65, 70, 50);
    }

    private static Border BuildSkeletonCard()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new Border { Height = 16, Width = 160, CornerRadius = new CornerRadius(4), Background = Theme.B(Theme.Surface3), HorizontalAlignment = HorizontalAlignment.Left });
        panel.Children.Add(new Border { Height = 10, Width = 100, CornerRadius = new CornerRadius(3), Background = Theme.B(Theme.Border), HorizontalAlignment = HorizontalAlignment.Left });
        panel.Children.Add(new Border { Height = 6,  CornerRadius = new CornerRadius(3), Background = Theme.B(Theme.Border) });
        return NxCard.Standard(panel);
    }

    private static string AbbreviatePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "—";
        // Show drive + last two folders max
        var parts = path.TrimEnd('\\', '/').Split('\\', '/');
        if (parts.Length <= 3) return path;
        return $"{parts[0]}\\…\\{parts[^2]}\\{parts[^1]}";
    }

    private static StackPanel HwStat(string label, string val, Color color)
    {
        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(Theme.Overline(label));
        var tb = Theme.Body(val, color);
        tb.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
        tb.MaxLines = 1;
        sp.Children.Add(tb);
        return sp;
    }

    private static Grid SettingRow(string name, string val, string reason)
    {
        var g = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var n = Theme.Caption(name); n.VerticalAlignment = VerticalAlignment.Center;
        var v = Theme.H3(val, Theme.Accent); v.VerticalAlignment = VerticalAlignment.Center;
        var r = Theme.Caption(reason, Theme.TextMuted); r.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(n, 0); Grid.SetColumn(v, 1); Grid.SetColumn(r, 2);
        g.Children.Add(n); g.Children.Add(v); g.Children.Add(r);
        return g;
    }
}
