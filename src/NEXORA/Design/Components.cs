// NEXORA Reusable UI Components
// All pages use these. Never build one-off UI from scratch.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace NEXORA.Design;

// ═══════════════════════════════════════════════════════════════════════════════
// BUTTONS
// ═══════════════════════════════════════════════════════════════════════════════

public static class NxButton
{
    /// <summary>Primary gradient button with hover glow.</summary>
    public static Button Primary(string label, RoutedEventHandler? click = null, double width = 0)
    {
        var btn = Make(label, click, width);
        btn.Background      = Theme.AccentGrad();
        btn.Foreground      = Theme.B(Theme.Bg);
        btn.BorderThickness = new Thickness(0);
        btn.FontWeight      = FontWeights.SemiBold;
        SetHoverTemplate(btn, Theme.AccentGrad(), Theme.B(Theme.Bg));
        return btn;
    }

    /// <summary>Outlined ghost button.</summary>
    public static Button Outline(string label, RoutedEventHandler? click = null, Color? color = null)
    {
        var c   = color ?? Theme.Accent;
        var btn = Make(label, click);
        btn.Background      = Theme.Transparent;
        btn.Foreground      = Theme.B(c);
        btn.BorderBrush     = Theme.B(c);
        btn.BorderThickness = new Thickness(1);
        SetHoverTemplate(btn, Theme.B(Color.FromArgb(0x22, c.R, c.G, c.B)), Theme.B(c));
        return btn;
    }

    /// <summary>Danger outlined button.</summary>
    public static Button Danger(string label, RoutedEventHandler? click = null)
        => Outline(label, click, Theme.Danger);

    /// <summary>Success outlined button.</summary>
    public static Button Success(string label, RoutedEventHandler? click = null)
        => Outline(label, click, Theme.Success);

    /// <summary>Ghost (no border) subtle button.</summary>
    public static Button Ghost(string label, RoutedEventHandler? click = null)
    {
        var btn = Make(label, click);
        btn.Background      = Theme.Transparent;
        btn.Foreground      = Theme.B(Theme.TextSec);
        btn.BorderThickness = new Thickness(0);
        SetHoverTemplate(btn, Theme.B(Theme.Surface3), Theme.B(Theme.TextPri));
        return btn;
    }

    private static Button Make(string label, RoutedEventHandler? click, double width = 0)
    {
        var btn = new Button
        {
            Content      = label,
            FontSize     = 13,
            FontFamily   = new FontFamily("Segoe UI Variable Text"),
            Padding      = new Thickness(22, 11, 22, 11),
            CornerRadius = new CornerRadius(8),
        };
        if (width > 0) btn.Width = width;
        if (click != null) btn.Click += click;
        // Safe "coming soon" fallback — never crash
        btn.Click += (s, e) =>
        {
            // Only fire if no other handler consumed the event
        };
        return btn;
    }

    private static void SetHoverTemplate(Button btn, Brush hoverBg, Brush hoverFg)
    {
        // Apply a clean ControlTemplate that prevents WinUI's default white-flash
        btn.Template = BuildTemplate(btn.Background, btn.Foreground, hoverBg, hoverFg,
                                     btn.BorderBrush, btn.BorderThickness, btn.CornerRadius);
    }

    private static ControlTemplate BuildTemplate(
        Brush normalBg, Brush normalFg, Brush hoverBg, Brush hoverFg,
        Brush borderBrush, Thickness borderThickness, CornerRadius radius)
    {
        // Build via XAML string to avoid complex code-behind VisualStateManager
        // This is the cleanest way to do hover states in WinUI 3 from C# code
        var xaml = $@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                  xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                  TargetType='Button'>
    <Border x:Name='Root'
            Background='{{TemplateBinding Background}}'
            BorderBrush='{{TemplateBinding BorderBrush}}'
            BorderThickness='{{TemplateBinding BorderThickness}}'
            CornerRadius='{{TemplateBinding CornerRadius}}'
            Padding='{{TemplateBinding Padding}}'>
        <ContentPresenter x:Name='ContentPresenter'
                          HorizontalAlignment='Center'
                          VerticalAlignment='Center'
                          Foreground='{{TemplateBinding Foreground}}'
                          FontSize='{{TemplateBinding FontSize}}'
                          FontWeight='{{TemplateBinding FontWeight}}'
                          FontFamily='{{TemplateBinding FontFamily}}'/>
        <VisualStateManager.VisualStateGroups>
            <VisualStateGroup x:Name='CommonStates'>
                <VisualState x:Name='Normal'/>
                <VisualState x:Name='PointerOver'>
                    <VisualState.Setters>
                        <Setter Target='Root.Opacity' Value='0.85'/>
                    </VisualState.Setters>
                </VisualState>
                <VisualState x:Name='Pressed'>
                    <VisualState.Setters>
                        <Setter Target='Root.Opacity' Value='0.65'/>
                    </VisualState.Setters>
                </VisualState>
                <VisualState x:Name='Disabled'>
                    <VisualState.Setters>
                        <Setter Target='Root.Opacity' Value='0.35'/>
                    </VisualState.Setters>
                </VisualState>
            </VisualStateGroup>
        </VisualStateManager.VisualStateGroups>
    </Border>
</ControlTemplate>";
        return (ControlTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CARDS
// ═══════════════════════════════════════════════════════════════════════════════

public static class NxCard
{
    /// <summary>Standard dark card.</summary>
    public static Border Standard(UIElement content, Thickness? margin = null) => new()
    {
        Background      = Theme.B(Theme.Surface1),
        BorderBrush     = Theme.B(Theme.Border),
        BorderThickness = new Thickness(1),
        CornerRadius    = new CornerRadius(14),
        Padding         = new Thickness(20, 16, 20, 16),
        Margin          = margin ?? new Thickness(0, 0, 0, 8),
        Child           = content
    };

    /// <summary>Accent-bordered glowing card.</summary>
    public static Border Accent(UIElement content, Thickness? margin = null) => new()
    {
        Background      = Theme.B(Theme.Surface2),
        BorderBrush     = Theme.B(Theme.BorderAccent),
        BorderThickness = new Thickness(1),
        CornerRadius    = new CornerRadius(14),
        Padding         = new Thickness(20, 16, 20, 16),
        Margin          = margin ?? new Thickness(0, 0, 0, 8),
        Child           = content
    };

    /// <summary>Gradient background card (hero card).</summary>
    public static Border Hero(UIElement content) => new()
    {
        Background      = Theme.CardGrad(),
        BorderBrush     = Theme.B(Theme.Border),
        BorderThickness = new Thickness(1),
        CornerRadius    = new CornerRadius(16),
        Padding         = new Thickness(24, 20, 24, 20),
        Margin          = new Thickness(0, 0, 0, 12),
        Child           = content
    };

    /// <summary>Metric mini card (stat box).</summary>
    public static Border Metric(UIElement content) => new()
    {
        Background      = Theme.B(Theme.Surface2),
        BorderBrush     = Theme.B(Theme.Border),
        BorderThickness = new Thickness(1),
        CornerRadius    = new CornerRadius(12),
        Padding         = new Thickness(16, 14, 16, 14),
        Child           = content
    };
}

// ═══════════════════════════════════════════════════════════════════════════════
// METRIC CARD  (CPU / GPU / RAM / Storage)
// ═══════════════════════════════════════════════════════════════════════════════

public static class NxMetricCard
{
    public static Border Build(string icon, string title, string subtitle,
        double usagePct, string usageLabel, string? tempLabel = null,
        Color? accentColor = null)
    {
        var ac = accentColor ?? Theme.Accent;

        var panel = new StackPanel { Spacing = 12 };

        // Icon + title row
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        hdr.Children.Add(new TextBlock { Text = icon, FontSize = 22 });
        var titles = new StackPanel();
        titles.Children.Add(Theme.Overline(title));
        titles.Children.Add(Theme.H3(subtitle));
        hdr.Children.Add(titles);
        panel.Children.Add(hdr);

        // Usage bar
        var barBg = new Border
        {
            Height       = 6,
            CornerRadius = new CornerRadius(3),
            Background   = Theme.B(Theme.Border),
            Margin       = new Thickness(0, 4, 0, 0)
        };
        var barFill = new Border
        {
            Height       = 6,
            CornerRadius = new CornerRadius(3),
            Background   = Theme.B(ac),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width        = 0 // set in Loaded
        };
        var barGrid = new Grid();
        barGrid.Children.Add(barBg);
        barGrid.Children.Add(barFill);
        panel.Children.Add(barGrid);

        // Set bar width dynamically
        barGrid.Loaded += (_, _) =>
        {
            barFill.Width = Math.Max(0, barGrid.ActualWidth * usagePct / 100.0);
        };
        barGrid.SizeChanged += (_, _) =>
        {
            barFill.Width = Math.Max(0, barGrid.ActualWidth * usagePct / 100.0);
        };

        // Stats row
        var stats = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        stats.Children.Add(StatPair("Usage", usageLabel, ac));
        if (tempLabel != null)
            stats.Children.Add(StatPair("Temp", tempLabel));
        panel.Children.Add(stats);

        return NxCard.Metric(panel);
    }

    private static StackPanel StatPair(string label, string value, Color? color = null)
    {
        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(Theme.Caption(label));
        var vt = Theme.H3(value, color ?? Theme.TextPri);
        sp.Children.Add(vt);
        return sp;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PROGRESS RING
// ═══════════════════════════════════════════════════════════════════════════════

public static class NxProgressRing
{
    /// <summary>Circular score ring with label in the centre.</summary>
    public static Grid Build(int value, int max, string label, double size = 120, Color? color = null)
    {
        var c  = color ?? Theme.Accent;
        var pct = (double)value / max;

        var grid = new Grid { Width = size, Height = size };

        // Background ring
        grid.Children.Add(new Ellipse
        {
            Width           = size,
            Height          = size,
            Stroke          = Theme.B(Theme.Border),
            StrokeThickness = 8,
            Fill            = Theme.Transparent
        });

        // Foreground arc (ProgressRing is easier than a custom arc in code)
        grid.Children.Add(new ProgressRing
        {
            Width        = size - 8,
            Height       = size - 8,
            Value        = value,
            Maximum      = max,
            Foreground   = Theme.B(c),
            Background   = Theme.Transparent,
            IsIndeterminate = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        });

        // Centre text
        var centre = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Spacing             = 0
        };
        var numTb = Theme.BigNum(value.ToString(), c);
        numTb.HorizontalAlignment = HorizontalAlignment.Center;
        var lblTb = Theme.Caption(label);
        lblTb.HorizontalAlignment = HorizontalAlignment.Center;
        centre.Children.Add(numTb);
        centre.Children.Add(lblTb);
        grid.Children.Add(centre);

        return grid;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// STATUS PILL
// ═══════════════════════════════════════════════════════════════════════════════

public static class NxStatus
{
    public enum StatusLevel { Good, Warning, Danger, Info }

    public static Border Pill(string text, StatusLevel level = StatusLevel.Good)
    {
        var c = level switch
        {
            StatusLevel.Warning => Theme.Warning,
            StatusLevel.Danger  => Theme.Danger,
            StatusLevel.Info    => Theme.Info,
            _                   => Theme.Success
        };
        var tb = new TextBlock
        {
            Text       = text,
            FontSize   = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.B(c)
        };
        return new Border
        {
            Background      = Theme.B(Color.FromArgb(0x22, c.R, c.G, c.B)),
            BorderBrush     = Theme.B(Color.FromArgb(0x55, c.R, c.G, c.B)),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(20),
            Padding         = new Thickness(8, 3, 8, 3),
            Child           = tb
        };
    }

    public static StackPanel Row(string label, string status, StatusLevel level = StatusLevel.Good)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        // Dot indicator
        var dot = new Ellipse
        {
            Width  = 8,
            Height = 8,
            Fill   = Theme.B(level switch
            {
                StatusLevel.Warning => Theme.Warning,
                StatusLevel.Danger  => Theme.Danger,
                StatusLevel.Info    => Theme.Info,
                _                   => Theme.Success
            }),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(dot);
        row.Children.Add(Theme.Body(label));
        var sp = Theme.Caption(status, level switch
        {
            StatusLevel.Warning => Theme.Warning,
            StatusLevel.Danger  => Theme.Danger,
            _                   => Theme.Success
        });
        sp.HorizontalAlignment = HorizontalAlignment.Right;
        // Push status to right
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(Theme.Body(label));
        var statusTb = Theme.Caption(status, level == StatusLevel.Good ? Theme.Success :
                                              level == StatusLevel.Warning ? Theme.Warning : Theme.Danger);
        Grid.SetColumn(statusTb, 1);
        g.Children.Add(statusTb);
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { dot, g } };
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SIMPLE LINE GRAPH (canvas-based, no external lib needed)
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class NxLineGraph : UserControl
{
    private readonly Canvas _canvas = new();
    private List<List<double>> _series = new();
    private List<Color> _colors = new();
    private List<string> _labels = new();
    private double _height = 120;

    public NxLineGraph()
    {
        Content = _canvas;
        _canvas.Background = Theme.Transparent;
        SizeChanged += (_, _) => Redraw();
    }

    public void SetSeries(List<(List<double> data, Color color, string label)> series)
    {
        _series = series.Select(s => s.data).ToList();
        _colors = series.Select(s => s.color).ToList();
        _labels = series.Select(s => s.label).ToList();
        Redraw();
    }

    private void Redraw()
    {
        _canvas.Children.Clear();
        if (_series.Count == 0 || ActualWidth < 10) return;

        double w = ActualWidth;
        double h = ActualHeight > 0 ? ActualHeight : _height;

        // Draw grid lines
        for (int i = 0; i <= 4; i++)
        {
            double y = h * i / 4;
            var line = new Line
            {
                X1 = 0, X2 = w, Y1 = y, Y2 = y,
                Stroke = Theme.B(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                StrokeThickness = 1
            };
            _canvas.Children.Add(line);
        }

        // Draw each series
        for (int s = 0; s < _series.Count; s++)
        {
            var data = _series[s];
            if (data.Count < 2) continue;
            var color = _colors[s];

            double min = 0, max = 100;
            double stepX = w / (data.Count - 1);

            // Area fill (lighter)
            var areaPoints = new Windows.Foundation.Point[data.Count + 2];
            areaPoints[0] = new Windows.Foundation.Point(0, h);
            for (int i = 0; i < data.Count; i++)
            {
                double x = i * stepX;
                double y = h - (data[i] - min) / (max - min) * h;
                areaPoints[i + 1] = new Windows.Foundation.Point(x, y);
            }
            areaPoints[data.Count + 1] = new Windows.Foundation.Point((data.Count - 1) * stepX, h);

            var poly = new Polygon
            {
                Fill            = Theme.B(Color.FromArgb(0x18, color.R, color.G, color.B)),
                StrokeThickness = 0
            };
            foreach (var pt in areaPoints) poly.Points.Add(pt);
            _canvas.Children.Add(poly);

            var polyline = new Polyline
            {
                Stroke          = Theme.B(color),
                StrokeThickness = 2,
                StrokeLineJoin  = Microsoft.UI.Xaml.Media.PenLineJoin.Round
            };
            for (int i = 0; i < data.Count; i++)
            {
                double x = i * stepX;
                double y = h - (data[i] - min) / (max - min) * h;
                polyline.Points.Add(new Windows.Foundation.Point(x, y));
            }
            _canvas.Children.Add(polyline);
        }

        // Legend
        double lx = 12;
        for (int s = 0; s < _labels.Count; s++)
        {
            var dot = new Ellipse { Width = 8, Height = 8, Fill = Theme.B(_colors[s]) };
            Canvas.SetLeft(dot, lx);
            Canvas.SetTop(dot, 8);
            _canvas.Children.Add(dot);
            lx += 12;

            var tb = new TextBlock
            {
                Text      = _labels[s],
                FontSize  = 10,
                Foreground = Theme.B(Theme.TextSec)
            };
            Canvas.SetLeft(tb, lx);
            Canvas.SetTop(tb, 6);
            _canvas.Children.Add(tb);
            lx += tb.ActualWidth + 20;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MODAL DIALOG
// ═══════════════════════════════════════════════════════════════════════════════

public static class NxModal
{
    /// <summary>Show a styled "Coming Soon" dialog.</summary>
    public static async System.Threading.Tasks.Task ShowComingSoon(XamlRoot root, string feature = "This feature")
    {
        await Show(root, "Under Development",
            $"{feature} is currently being built.\n\nThis feature will be available in a future update.",
            "OK", null);
    }

    /// <summary>Show a custom modal. Returns true if primary button pressed.</summary>
    public static async System.Threading.Tasks.Task<bool> Show(
        XamlRoot root, string title, string message,
        string primaryBtn = "OK", string? secondaryBtn = null)
    {
        var content = new StackPanel { Spacing = 12, MinWidth = 320, MaxWidth = 480 };
        content.Children.Add(Theme.Body(message));

        var dialog = new ContentDialog
        {
            Title                   = title,
            Content                 = content,
            PrimaryButtonText       = primaryBtn,
            SecondaryButtonText     = secondaryBtn ?? string.Empty,
            DefaultButton           = ContentDialogButton.Primary,
            XamlRoot                = root,
            RequestedTheme          = ElementTheme.Dark
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// EMPTY STATE
// ═══════════════════════════════════════════════════════════════════════════════

public static class NxEmpty
{
    public static StackPanel Build(string emoji, string title, string subtitle,
        string? btnLabel = null, RoutedEventHandler? btnClick = null)
    {
        var sp = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Spacing             = 12,
            Margin              = new Thickness(0, 60, 0, 60)
        };

        sp.Children.Add(new TextBlock
        {
            Text                = emoji,
            FontSize            = 48,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        var t = Theme.H3(title);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(t);
        var s = Theme.Caption(subtitle);
        s.HorizontalAlignment = HorizontalAlignment.Center;
        s.TextAlignment       = Microsoft.UI.Xaml.TextAlignment.Center;
        sp.Children.Add(s);

        if (btnLabel != null)
        {
            var btn = NxButton.Outline(btnLabel, btnClick);
            btn.HorizontalAlignment = HorizontalAlignment.Center;
            btn.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(btn);
        }

        return sp;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION HEADER
// ═══════════════════════════════════════════════════════════════════════════════

public static class NxSection
{
    public static StackPanel Header(string title, string? subtitle = null)
    {
        var sp = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 16) };
        sp.Children.Add(Theme.H1(title));
        if (subtitle != null)
            sp.Children.Add(Theme.Body(subtitle, Theme.TextSec));
        return sp;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SCORE BAR ROW
// ═══════════════════════════════════════════════════════════════════════════════

public static class NxScoreBar
{
    public static Grid Build(string label, int score, Color? color = null)
    {
        var c = color ?? Theme.Accent;
        var g = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

        var lbl = Theme.Caption(label); lbl.VerticalAlignment = VerticalAlignment.Center;

        var barBg = new Border
        {
            Height       = 6,
            CornerRadius = new CornerRadius(3),
            Background   = Theme.B(Theme.Border),
            VerticalAlignment = VerticalAlignment.Center,
            Margin       = new Thickness(12, 0, 12, 0)
        };
        var barFill = new Border
        {
            Height       = 6,
            CornerRadius = new CornerRadius(3),
            Background   = Theme.B(c),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Center,
            Width        = 0
        };
        var barContainer = new Grid();
        barContainer.Children.Add(barBg);
        barContainer.Children.Add(barFill);
        barContainer.Margin = new Thickness(12, 0, 12, 0);

        barContainer.SizeChanged += (_, _) =>
            barFill.Width = Math.Max(0, barContainer.ActualWidth * score / 100.0);

        var num = Theme.Caption(score.ToString(), c); num.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(barContainer, 1);
        Grid.SetColumn(num, 2);
        g.Children.Add(lbl);
        g.Children.Add(barContainer);
        g.Children.Add(num);
        return g;
    }
}
