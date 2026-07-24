namespace PatarikAIOS;

public sealed class SupplierStatusWindow : Window
{
    private readonly ComboBox _status = new() { MinWidth = 330 };
    private readonly TextBox _note = new() { MinWidth = 330, MinHeight = 70, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    private readonly string _previous;
    public SupplierStatusChange? Result { get; private set; }

    public SupplierStatusWindow(DateOnly date, string supplier, string previous)
    {
        _previous = previous;
        Title = "Փոխել մատակարարի կարգավիճակը"; Width = 470; Height = 350; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var value in new[] { "Սպասվում է", "Պատվիրված է", "Կատարված է", "Չի եկել", "Խնդիր" }) _status.Items.Add(value);
        _status.SelectedItem = previous;
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = $"{supplier} · {date:dd.MM.yyyy}", FontSize = 16, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = $"Նախորդ կարգավիճակ՝ {previous}", Margin = new Thickness(0, 8, 0, 10) });
        panel.Children.Add(new TextBlock { Text = "Նոր կարգավիճակ", FontWeight = FontWeights.SemiBold }); panel.Children.Add(_status);
        panel.Children.Add(new TextBlock { Text = "Պատճառ / դիտողություն", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) }); panel.Children.Add(_note);
        var save = new Button { Content = "Պահպանել փոփոխությունը", Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White, IsDefault = true, Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) => { Result = new SupplierStatusChange(date, supplier, _previous, _status.SelectedItem?.ToString() ?? _previous, _note.Text.Trim(), DateTime.Now, "Տնօրեն՝ ձեռքով"); DialogResult = true; };
        panel.Children.Add(save); Content = panel;
    }
}
