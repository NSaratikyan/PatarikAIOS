using System.Windows;
using System.Windows.Controls;

namespace PatarikAIOS;

public sealed class AgentChatWindow : Window
{
    private readonly BusinessManagerAgentClient _client = new();
    private readonly DashboardSnapshot _snapshot;
    private readonly TextBox _conversation;
    private readonly TextBox _input;
    private readonly Button _sendButton;

    public AgentChatWindow(DashboardSnapshot snapshot)
    {
        _snapshot = snapshot;
        Title = "Patarik AI Manager";
        Width = 760;
        Height = 620;
        MinWidth = 620;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = "🤖 Patarik AI Manager", FontSize = 22, FontWeight = FontWeights.Bold });
        header.Children.Add(new TextBlock
        {
            Text = "Վերլուծում է միայն ծրագրում առկա և հաշվարկված տվյալները։ Ֆինանսական գործողություններ չի կատարում։",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _conversation = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(12),
            Text = "AI Manager-ը պատրաստ է։ Օրինակ՝ գրեք՝ «Ի՞նչ խնդիրների վրա կենտրոնանամ այսօր»։\n"
        };
        Grid.SetRow(_conversation, 1);
        root.Children.Add(_conversation);

        var composer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        composer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        composer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _input = new TextBox
        {
            MinHeight = 42,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 10, 0)
        };
        _input.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                await SendAsync();
            }
        };
        Grid.SetColumn(_input, 0);
        composer.Children.Add(_input);

        _sendButton = new Button
        {
            Content = "Ուղարկել",
            MinWidth = 100,
            Padding = new Thickness(14, 8, 14, 8)
        };
        _sendButton.Click += async (_, _) => await SendAsync();
        Grid.SetColumn(_sendButton, 1);
        composer.Children.Add(_sendButton);

        Grid.SetRow(composer, 2);
        root.Children.Add(composer);
        Content = root;
    }

    private async Task SendAsync()
    {
        var message = _input.Text.Trim();
        if (string.IsNullOrWhiteSpace(message)) return;

        _sendButton.IsEnabled = false;
        _input.IsEnabled = false;
        _conversation.AppendText($"\nԴուք: {message}\n\nAI Manager: ");
        _conversation.ScrollToEnd();
        _input.Clear();

        try
        {
            var answer = await _client.AskAsync(message, _snapshot);
            _conversation.AppendText(answer + "\n");
        }
        catch (Exception ex)
        {
            _conversation.AppendText($"Չհաջողվեց կապվել AI Manager service-ի հետ։ {ex.Message}\n");
        }
        finally
        {
            _sendButton.IsEnabled = true;
            _input.IsEnabled = true;
            _input.Focus();
            _conversation.ScrollToEnd();
        }
    }
}
