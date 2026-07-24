namespace PatarikAIOS;

public sealed class TelegramBotSettingsWindow : Window
{
    private readonly PasswordBox _token;
    public TelegramBotSettings? Result { get; private set; }

    public TelegramBotSettingsWindow(TelegramBotSettings settings)
    {
        Title = "Telegram բոտի կարգավորում";
        Width = 560; Height = 285;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        _token = new PasswordBox { Password = settings.BotToken };
        var save = new Button { Content = "Պահպանել և միացնել", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += (_, _) =>
        {
            var token = _token.Password.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Մուտքագրեք BotFather-ից ստացված բոտի բանալին։", "Telegram", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Result = new TelegramBotSettings(token);
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
                new TextBlock { Text = "Telegram բոտի միացում", FontSize = 20, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = "Նախ բոտին Telegram-ում ուղարկեք /start, հետո այստեղ տեղադրեք BotFather-ից ստացած բանալին։ Այն պահվում է միայն այս համակարգչի տեղային կարգավորումներում։", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 14) },
                new TextBlock { Text = "Bot Token", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 4) },
                _token,
                actions
            }
        };
    }
}
