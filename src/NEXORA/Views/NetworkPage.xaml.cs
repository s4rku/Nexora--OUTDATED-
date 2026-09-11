using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class NetworkPage : Page
{
    private NetworkViewModel? _vm;
    private StackPanel? _resultsPanel;
    private TextBlock?  _statusTb;
    private Canvas?     _graphCanvas;

    public NetworkPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            _vm = App.Services.GetRequiredService<NetworkViewModel>();
            _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
            Build();
        }
        catch (Exception ex) { App.Log($"NetworkPage crash: {ex}"); }
    }

    private void Build()
    {
        Root.Children.Clear();

        Root.Children.Add(NxSection.Header("Network",
            "Diagnose latency, jitter, packet loss, and adapter health."));

        // Important disclaimer
        Root.Children.Add(NxCard.Standard(new StackPanel { Spacing = 6, Children =
        {
            Theme.H3("What this measures"),
            Theme.Body("NEXORA measures real latency to public DNS servers (Google, Cloudflare, OpenDNS) and your DNS resolution speed.", Theme.TextSec),
            Theme.Caption("Note: Software cannot reduce your base ping — that is determined by your ISP and physical distance to servers.", Theme.TextMuted),
        }}));

        // Action button
        var diagBtn = new Button { Content = "RUN DIAGNOSTICS", Style = (Style)Application.Current.Resources["NxPrimaryBtn"] };
        diagBtn.Click += async (_, _) =>
        {
            try { diagBtn.IsEnabled = false; if (_vm != null) await _vm.RunTestAsync(); }
            finally { diagBtn.IsEnabled = true; }
        };
        Root.Children.Add(diagBtn);

        _statusTb = Theme.Caption("Click RUN DIAGNOSTICS to test your connection.", Theme.TextSec);
        Root.Children.Add(_statusTb);

        Root.Children.Add(Theme.Overline("RESULTS"));
        _resultsPanel = new StackPanel { Spacing = 10 };
        Root.Children.Add(_resultsPanel);
    }

    private void Refresh()
    {
        if (_vm == null || _resultsPanel == null) return;
        if (_statusTb != null) _statusTb.Text = _vm.StatusMessage;

        _resultsPanel.Children.Clear();
        if (_vm.Results == null) return;

        var r = _vm.Results;

        // Stat cards row
        var statGrid = new Grid { ColumnSpacing = 10 };
        for (int i = 0; i < 4; i++)
            statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var pingColor = r.PingMs < 20 ? Theme.Success : r.PingMs < 60 ? Theme.Warning : Theme.Danger;
        var jitColor  = r.JitterMs < 5 ? Theme.Success : r.JitterMs < 15 ? Theme.Warning : Theme.Danger;
        var plColor   = r.PacketLossPercent < 1 ? Theme.Success : Theme.Danger;
        var dnsColor  = r.DnsResponseMs < 50 ? Theme.Success : r.DnsResponseMs < 150 ? Theme.Warning : Theme.Danger;

        var sc0 = StatCard("PING",        $"{r.PingMs:F0} ms",           pingColor); Grid.SetColumn(sc0, 0); statGrid.Children.Add(sc0);
        var sc1 = StatCard("JITTER",      $"{r.JitterMs:F1} ms",         jitColor);  Grid.SetColumn(sc1, 1); statGrid.Children.Add(sc1);
        var sc2 = StatCard("PACKET LOSS", $"{r.PacketLossPercent:F1}%",  plColor);   Grid.SetColumn(sc2, 2); statGrid.Children.Add(sc2);
        var sc3 = StatCard("DNS RESPONSE",$"{r.DnsResponseMs:F0} ms",    dnsColor);  Grid.SetColumn(sc3, 3); statGrid.Children.Add(sc3);
        _resultsPanel.Children.Add(statGrid);

        // Adapter info
        if (!string.IsNullOrEmpty(r.AdapterName))
        {
            var adp = new StackPanel { Spacing = 8 };
            adp.Children.Add(Theme.Overline("ADAPTER"));
            var adpRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
            adpRow.Children.Add(AdpStat("Name",       r.AdapterName,       Theme.TextPri));
            adpRow.Children.Add(AdpStat("Type",       r.ConnectionType,    r.ConnectionType == "Ethernet" ? Theme.Success : Theme.Warning));
            adpRow.Children.Add(AdpStat("IP Address", r.IpAddress,         Theme.TextSec));
            adp.Children.Add(adpRow);
            _resultsPanel.Children.Add(NxCard.Standard(adp));
        }

        // Recommendations
        if (r.Recommendations?.Any() == true)
        {
            var recPanel = new StackPanel { Spacing = 8 };
            recPanel.Children.Add(Theme.Overline("RECOMMENDATIONS"));
            foreach (var rec in r.Recommendations)
            {
                var recRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                // Accent left border dot
                recRow.Children.Add(new Border
                {
                    Width = 3, CornerRadius = new CornerRadius(1.5),
                    Background = Theme.B(Theme.Accent),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Margin = new Thickness(0, 2, 0, 2)
                });
                recRow.Children.Add(Theme.Body(rec));
                recPanel.Children.Add(recRow);
            }
            _resultsPanel.Children.Add(NxCard.Standard(recPanel));
        }

        // History graph (throughput from LHWM if available)
        Root.Children.Add(Theme.Overline("THROUGHPUT HISTORY"));
        _graphCanvas = new Canvas { Height = 130 };
        _graphCanvas.SizeChanged += (_, _) => DrawGraph();
        _resultsPanel.Children.Add(NxCard.Standard(_graphCanvas));
        DrawGraph();
    }

    private void DrawGraph()
    {
        if (_graphCanvas == null) return;
        _graphCanvas.Children.Clear();
        double w = _graphCanvas.ActualWidth, h = 130;
        if (w < 10) return;

        // Grid lines
        for (int i = 1; i <= 3; i++)
        {
            double y = h * i / 4;
            _graphCanvas.Children.Add(new Line { X1=0, X2=w, Y1=y, Y2=y, Stroke=Theme.B(Color.FromArgb(0x10,0xFF,0xFF,0xFF)), StrokeThickness=1 });
        }
        DrawSeries(MockPerformanceData.NetDownHist, Theme.Accent,     w, h, "Download");
        DrawSeries(MockPerformanceData.NetUpHist,   Theme.AccentPurp, w, h, "Upload");
    }

    private void DrawSeries(List<double> data, Color color, double w, double h, string label)
    {
        if (data.Count < 2) return;
        double step = w / (data.Count - 1);
        var area = new Polygon { Fill = Theme.B(Color.FromArgb(0x16, color.R, color.G, color.B)), StrokeThickness = 0 };
        var line = new Polyline { Stroke = Theme.B(color), StrokeThickness = 1.6, StrokeLineJoin = Microsoft.UI.Xaml.Media.PenLineJoin.Round };
        area.Points.Add(new Windows.Foundation.Point(0, h));
        for (int i = 0; i < data.Count; i++)
        {
            double x = i * step, y = h - data[i] / 100.0 * h;
            line.Points.Add(new Windows.Foundation.Point(x, y));
            area.Points.Add(new Windows.Foundation.Point(x, y));
        }
        area.Points.Add(new Windows.Foundation.Point((data.Count - 1) * step, h));
        _graphCanvas!.Children.Add(area);
        _graphCanvas.Children.Add(line);
    }

    private static Border StatCard(string label, string val, Color color)
    {
        var sp = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(Theme.Overline(label));
        var valTb = Theme.BigNum(val, color); valTb.FontSize = 26; valTb.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(valTb);
        return NxCard.Metric(sp);
    }

    private static StackPanel AdpStat(string label, string val, Color color)
    {
        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(Theme.Caption(label));
        sp.Children.Add(Theme.Body(val, color));
        return sp;
    }
}
