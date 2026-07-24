namespace PatarikAIOS;

public sealed class EmployeeTelegramBotSettingsWindow : Window
{
    private readonly TextBox _username;
    private readonly PasswordBox _token;
    public EmployeeTelegramBotSettings? Result { get; private set; }

    public EmployeeTelegramBotSettingsWindow(EmployeeTelegramBotSettings settings)
    {
        Title = "Աշխատակիցների բոտի կարգավորումներ";
        Width = 560; Height = 330;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        _username = new TextBox { Text = settings.BotUsername };
        _token = new PasswordBox { Password = settings.BotToken };
        var save = new Button { Content = "Պահպանել", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += (_, _) =>
        {
            var username = _username.Text.Trim();
            var token = _token.Password.Trim();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Լրացրեք բոտի username-ը և BotFather-ից ստացված token-ը։", "Աշխատակիցների բոտ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Result = new EmployeeTelegramBotSettings(username, token);
            DialogResult = true;
        };

        var cancel = new Button { Content = "Չեղարկել", IsCancel = true };
        cancel.Click += (_, _) => Close();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        actions.Children.Add(cancel); actions.Children.Add(save);

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Children =
            {
                new TextBlock { Text = "Աշխատակիցների Telegram բոտ", FontSize = 20, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = "Այս բոտը նախատեսված է միայն պատվերների, ստացումների, վճարման կարգավիճակի և խնդիրների հաղորդման համար։ Այն չի ցուցադրի տնօրենի ֆինանսական տվյալները։", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 14) },
                new TextBlock { Text = "Bot username", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 4) },
                _username,
                new TextBlock { Text = "Bot Token", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 4) },
                _token,
                actions
            }
        };
    }
}
