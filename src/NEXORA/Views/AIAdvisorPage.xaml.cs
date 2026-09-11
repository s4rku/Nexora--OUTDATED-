using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using NEXORA.Design;
using NEXORA.ViewModels;
using Windows.UI;

namespace NEXORA.Views;

public sealed partial class AIAdvisorPage : Page
{
    private AIAdvisorViewModel? _vm;

    public AIAdvisorPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            _vm = App.Services.GetRequiredService<AIAdvisorViewModel>();
            _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(RefreshChat);

            // Header
            HeaderPanel.Children.Clear();
            HeaderPanel.Children.Add(Theme.H1("NEXORA AI"));
            HeaderPanel.Children.Add(Theme.Body(
                "Ask me about your system — FPS, temperatures, bottlenecks, or how to improve performance.",
                Theme.TextSec));

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            var analyseBtn = new Button { Content = "ANALYSE SYSTEM NOW", Style = (Style)Application.Current.Resources["NxOutlineBtn"] };
            analyseBtn.Click += async (_, _) =>
            {
                try
                {
                    analyseBtn.IsEnabled = false;
                    if (_vm != null) await _vm.AnalyzeNowAsync();
                }
                finally { analyseBtn.IsEnabled = true; }
            };
            btnRow.Children.Add(analyseBtn);

            var suggestionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var q in new[] { "Why is my FPS low?", "What's bottlenecking my PC?", "What are my temperatures?" })
            {
                var qBtn = new Button
                {
                    Content = q,
                    Style   = (Style)Application.Current.Resources["NxOutlineBtn"],
                    Padding = new Thickness(12, 6, 12, 6),
                    FontSize = 11
                };
                var capQ = q;
                qBtn.Click += (_, _) =>
                {
                    InputBox.Text = capQ;
                    Send();
                };
                suggestionsPanel.Children.Add(qBtn);
            }
            HeaderPanel.Children.Add(btnRow);
            HeaderPanel.Children.Add(suggestionsPanel);

            // Style send button — gradient
            SendBtn.Background = Theme.AccentGrad();
            SendBtn.Foreground = Theme.B(Theme.Bg);

            // Render seed messages
            RefreshChat();
        }
        catch (Exception ex) { App.Log($"AIAdvisorPage crash: {ex}"); }
    }

    private void SendBtn_Click(object sender, RoutedEventArgs e) => Send();

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) Send();
    }

    private void Send()
    {
        if (_vm == null || string.IsNullOrWhiteSpace(InputBox.Text)) return;
        _vm.UserInput = InputBox.Text;
        InputBox.Text = string.Empty;
        _ = _vm.SendMessageAsync();
    }

    private void RefreshChat()
    {
        if (_vm == null) return;
        ChatPanel.Children.Clear();
        foreach (var msg in _vm.ChatHistory)
            AddBubble(msg.Text, msg.IsUser, msg.Timestamp);
    }

    private void AddBubble(string text, bool isUser, DateTime time)
    {
        var bubble = new Border
        {
            Background      = Theme.B(isUser ? Theme.Surface3 : Theme.Surface2),
            BorderBrush     = Theme.B(isUser ? Theme.Border : Theme.BorderAccent),
            BorderThickness = new Thickness(1),
            CornerRadius    = isUser ? new CornerRadius(12, 4, 12, 12) : new CornerRadius(4, 12, 12, 12),
            Padding         = new Thickness(16, 12, 16, 12),
            Margin          = isUser
                ? new Thickness(100, 0, 0, 8)
                : new Thickness(0, 0, 100, 8),
            MaxWidth = 700
        };

        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text         = text,
            FontSize     = 13,
            Foreground   = Theme.B(Theme.TextPri),
            TextWrapping = TextWrapping.Wrap,
            LineHeight   = 20
        });
        content.Children.Add(Theme.Caption(time.ToString("HH:mm"), Theme.TextMuted));
        bubble.Child = content;

        var row = new StackPanel
        {
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };
        row.Children.Add(bubble);
        ChatPanel.Children.Add(row);
    }
}
