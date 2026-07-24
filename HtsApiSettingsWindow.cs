namespace PatarikAIOS;

public sealed class HtsApiSettingsWindow : Window
{
    private readonly TextBox _baseUrl;
    private readonly PasswordBox _apiKey;
    private readonly ComboBox _language;
    public HtsApiSettings? Result { get; private set; }

    public HtsApiSettingsWindow(HtsApiSettings settings)
    {
        Title = "ՀԾ API կարգավորումներ";
        Width = 580; Height = 390;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        _baseUrl = new TextBox { Text = string.IsNullOrWhiteSpace(settings.BaseUrl) ? HtsApiSettings.Empty.BaseUrl : settings.BaseUrl };
        _apiKey = new PasswordBox { Password = settings.ApiKey };
        _language = new ComboBox { ItemsSource = new[] { "hy-AM", "ru-RU", "en-US" }, SelectedItem = settings.Language };

        var save = new Button { Content = "Պահպանել և ստուգել կապը", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += Save_Click;
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true };
        cancel.Click += (_, _) => Close();

        var content = new StackPanel { Margin = new Thickness(24) };
        content.Children.Add(new TextBlock { Text = "ՀԾ-Առևտուր Cloud API", FontSize = 20, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = "Բանալին մուտքագրվում է միայն այս Windows համակարգչում և չի ավելացվում ծրագրի կոդին։", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 16) });
        content.Children.Add(Label("API հասցե")); content.Children.Add(_baseUrl);
        content.Children.Add(Label("API բանալի")); content.Children.Add(_apiKey);
        content.Children.Add(Label("Պատասխանի լեզու")); content.Children.Add(_language);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        actions.Children.Add(cancel); actions.Children.Add(save); content.Children.Add(actions);
        Content = content;
    }

    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) };

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var url = _baseUrl.Text.Trim();
        var key = _apiKey.Password.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out _) || string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show("Լրացրեք ճիշտ API հասցե և API բանալի։", "Չլրացված տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result = new HtsApiSettings(url, key, _language.SelectedItem as string ?? "hy-AM");
        DialogResult = true;
    }
}
