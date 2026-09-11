// Shared UI helpers used by all code-built pages.
// Using direct color values avoids Application.Current.Resources lookups
// which can fail if called before the page is in the visual tree.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NEXORA.Views;

internal static class UI
{
    // ── Colours ──────────────────────────────────────────────────────────
    public static readonly Color Accent    = Color.FromArgb(0xFF, 0x00, 0xC8, 0xFF);
    public static readonly Color Surface   = Color.FromArgb(0xFF, 0x1E, 0x21, 0x28);
    public static readonly Color Border    = Color.FromArgb(0xFF, 0x2A, 0x2D, 0x38);
    public static readonly Color TextPri   = Color.FromArgb(0xFF, 0xF0, 0xF2, 0xF5);
    public static readonly Color TextSec   = Color.FromArgb(0xFF, 0x8B, 0x95, 0xA8);
    public static readonly Color TextMuted = Color.FromArgb(0xFF, 0x4A, 0x55, 0x68);
    public static readonly Color Success   = Color.FromArgb(0xFF, 0x00, 0xE6, 0x76);
    public static readonly Color Warning   = Color.FromArgb(0xFF, 0xFF, 0xC1, 0x07);
    public static readonly Color Danger    = Color.FromArgb(0xFF, 0xFF, 0x44, 0x44);
    public static readonly Color BgMain    = Color.FromArgb(0xFF, 0x0C, 0x0D, 0x10);

    public static SolidColorBrush Brush(Color c) => new(c);

    // ── Text ─────────────────────────────────────────────────────────────
    public static TextBlock H1(string text) => new()
    {
        Text       = text,
        FontFamily = new FontFamily("Segoe UI Variable Display"),
        FontSize   = 28,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brush(TextPri),
        Margin     = new Thickness(0, 0, 0, 4)
    };

    public static TextBlock Label(string text, bool accent = false) => new()
    {
        Text        = text,
        FontSize    = 13,
        Foreground  = Brush(accent ? Accent : TextPri),
        TextWrapping = TextWrapping.Wrap
    };

    public static TextBlock Small(string text, Color? color = null) => new()
    {
        Text        = text,
        FontSize    = 11,
        Foreground  = Brush(color ?? TextSec),
        TextWrapping = TextWrapping.Wrap
    };

    public static TextBlock SectionHdr(string text) => new()
    {
        Text             = text.ToUpperInvariant(),
        FontSize         = 10,
        FontWeight       = FontWeights.SemiBold,
        Foreground       = Brush(TextSec),
        CharacterSpacing = 80,
        Margin           = new Thickness(0, 12, 0, 4)
    };

    // ── Containers ────────────────────────────────────────────────────────
    public static Border Card(UIElement content, bool accent = false) => new()
    {
        Background      = Brush(Surface),
        BorderBrush     = Brush(accent ? Accent : Border),
        BorderThickness = new Thickness(1, 1, 1, 1),
        CornerRadius    = new CornerRadius(12),
        Padding         = new Thickness(20, 16, 20, 16),
        Margin          = new Thickness(0, 0, 0, 6),
        Child           = content
    };

    // ── Buttons ───────────────────────────────────────────────────────────
    public static Button PrimaryBtn(string text, RoutedEventHandler onClick)
    {
        var grad = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 0) };
        grad.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Accent, Offset = 0 });
        grad.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = Color.FromArgb(0xFF, 0x7B, 0x2F, 0xFF), Offset = 1 });
        var btn = new Button { Content = text, FontSize = 13, FontWeight = FontWeights.SemiBold, Padding = new Thickness(24, 12, 24, 12), CornerRadius = new CornerRadius(8), Background = grad, Foreground = Brush(BgMain), BorderThickness = new Thickness(0, 0, 0, 0) };
        btn.Click += onClick;
        return btn;
    }

    public static Button OutlineBtn(string text, RoutedEventHandler onClick) => MakeBtn(text, onClick, Accent, false);
    public static Button DangerBtn(string text, RoutedEventHandler onClick)  => MakeBtn(text, onClick, Danger, false);

    private static Button MakeBtn(string text, RoutedEventHandler onClick, Color color, bool fill)
    {
        var btn = new Button { Content = text, FontSize = 13, FontWeight = FontWeights.SemiBold, Padding = new Thickness(20, 10, 20, 10), CornerRadius = new CornerRadius(8), Background = fill ? Brush(color) : Brush(Color.FromArgb(0, 0, 0, 0)), Foreground = Brush(fill ? BgMain : color), BorderBrush = Brush(color), BorderThickness = new Thickness(1, 1, 1, 1) };
        btn.Click += onClick;
        return btn;
    }

    // ── Progress bar ─────────────────────────────────────────────────────
    public static ProgressBar Bar(double value = 0, double max = 100) => new()
    {
        Value        = value,
        Maximum      = max,
        Height       = 6,
        CornerRadius = new CornerRadius(3),
        Foreground   = Brush(Accent),
        Background   = Brush(Border)
    };

    // ── Separator ─────────────────────────────────────────────────────────
    public static Border Sep() => new()
    {
        Height     = 1,
        Background = Brush(Border),
        Margin     = new Thickness(0, 4, 0, 4)
    };
}
