// NEXORA Design System — single source of truth for all colours, sizes, and spacing.
// All pages and components reference this class — no hardcoded values elsewhere.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using Windows.UI.Text;

namespace NEXORA.Design;

public static class Theme
{
    // ── Brand colours ────────────────────────────────────────────────────
    public static readonly Color Accent      = Color.FromArgb(0xFF, 0x00, 0xC8, 0xFF); // #00C8FF
    public static readonly Color AccentPurp  = Color.FromArgb(0xFF, 0x7B, 0x2F, 0xFF); // #7B2FFF
    public static readonly Color AccentGlow  = Color.FromArgb(0x33, 0x00, 0xC8, 0xFF); // glow ring
    public static readonly Color Success     = Color.FromArgb(0xFF, 0x00, 0xE6, 0x76); // #00E676
    public static readonly Color Warning     = Color.FromArgb(0xFF, 0xFF, 0xC1, 0x07); // #FFC107
    public static readonly Color Danger      = Color.FromArgb(0xFF, 0xFF, 0x44, 0x44); // #FF4444
    public static readonly Color Info        = Color.FromArgb(0xFF, 0x64, 0xFF, 0xDA); // #64FFDA

    // ── Surface colours ──────────────────────────────────────────────────
    public static readonly Color Bg          = Color.FromArgb(0xFF, 0x09, 0x0A, 0x0F); // deep bg
    public static readonly Color Surface0    = Color.FromArgb(0xFF, 0x0E, 0x10, 0x18); // sidebar
    public static readonly Color Surface1    = Color.FromArgb(0xFF, 0x13, 0x15, 0x1E); // base card
    public static readonly Color Surface2    = Color.FromArgb(0xFF, 0x1A, 0x1D, 0x28); // elevated card
    public static readonly Color Surface3    = Color.FromArgb(0xFF, 0x21, 0x25, 0x32); // hover
    public static readonly Color Border      = Color.FromArgb(0xFF, 0x28, 0x2D, 0x40); // card border
    public static readonly Color BorderAccent= Color.FromArgb(0x44, 0x00, 0xC8, 0xFF); // accent border

    // ── Text colours ─────────────────────────────────────────────────────
    public static readonly Color TextPri     = Color.FromArgb(0xFF, 0xF0, 0xF2, 0xF8); // primary
    public static readonly Color TextSec     = Color.FromArgb(0xFF, 0x88, 0x94, 0xAA); // secondary
    public static readonly Color TextMuted   = Color.FromArgb(0xFF, 0x45, 0x50, 0x65); // muted
    public static readonly Color TextAccent  = Color.FromArgb(0xFF, 0x00, 0xC8, 0xFF); // accent

    // ── Sidebar ──────────────────────────────────────────────────────────
    public static readonly Color SidebarBg      = Color.FromArgb(0xFF, 0x0B, 0x0D, 0x14);
    public static readonly Color SidebarHover   = Color.FromArgb(0x14, 0x00, 0xC8, 0xFF);
    public static readonly Color SidebarActive  = Color.FromArgb(0x22, 0x00, 0xC8, 0xFF);
    public static readonly Color SidebarActiveBorder = Color.FromArgb(0xFF, 0x00, 0xC8, 0xFF);

    // ── Brushes ──────────────────────────────────────────────────────────
    public static SolidColorBrush B(Color c)   => new(c);
    public static SolidColorBrush Transparent  => new(Color.FromArgb(0, 0, 0, 0));

    public static LinearGradientBrush AccentGrad(double angle = 90)
    {
        var g = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint   = new Windows.Foundation.Point(1, 0)
        };
        g.GradientStops.Add(new GradientStop { Color = Accent,     Offset = 0 });
        g.GradientStops.Add(new GradientStop { Color = AccentPurp, Offset = 1 });
        return g;
    }

    public static LinearGradientBrush CardGrad() 
    {
        var g = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint   = new Windows.Foundation.Point(0, 1)
        };
        g.GradientStops.Add(new GradientStop { Color = Surface2, Offset = 0 });
        g.GradientStops.Add(new GradientStop { Color = Surface1, Offset = 1 });
        return g;
    }

    // ── Typography helpers ───────────────────────────────────────────────
    public static TextBlock H1(string t, Color? color = null) => Txt(t, 28, FontWeights.SemiBold, color ?? TextPri);
    public static TextBlock H2(string t, Color? color = null) => Txt(t, 20, FontWeights.SemiBold, color ?? TextPri);
    public static TextBlock H3(string t, Color? color = null) => Txt(t, 15, FontWeights.SemiBold, color ?? TextPri);
    public static TextBlock Body(string t, Color? color = null) => Txt(t, 13, FontWeights.Normal, color ?? TextPri);
    public static TextBlock Caption(string t, Color? color = null) => Txt(t, 11, FontWeights.Normal, color ?? TextSec);
    public static TextBlock Overline(string t) { var tb = Txt(t, 10, FontWeights.SemiBold, TextSec); tb.CharacterSpacing = 100; return tb; }
    public static TextBlock BigNum(string t, Color? color = null) => Txt(t, 40, FontWeights.Light, color ?? TextPri, "Segoe UI Variable Display");
    public static TextBlock MedNum(string t, Color? color = null) => Txt(t, 24, FontWeights.Light, color ?? TextPri, "Segoe UI Variable Display");

    private static TextBlock Txt(string text, double size, FontWeight weight, Color color, string? font = null) => new()
    {
        Text         = text,
        FontSize     = size,
        FontWeight   = weight,
        Foreground   = B(color),
        TextWrapping = TextWrapping.Wrap,
        FontFamily   = font != null ? new FontFamily(font) : new FontFamily("Segoe UI Variable Text")
    };

    // ── Separators ───────────────────────────────────────────────────────
    public static Border HSep(double opacity = 1.0) => new()
    {
        Height      = 1,
        Background  = B(Border),
        Margin      = new Thickness(0, 8, 0, 8),
        Opacity     = opacity
    };
}
