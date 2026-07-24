using System.Globalization;

namespace PatarikAIOS;

public sealed class CompletedPaymentWindow : Window
{
    private readonly ComboBox _recipient = new() { IsEditable = true, MinWidth = 280 };
    private readonly TextBox _amount = new() { MinWidth = 280 };
    private readonly DatePicker _date;
    private readonly TextBox _note = new() { MinWidth = 280, MinHeight = 60, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
    public CompletedPayment? Result { get; private set; }

    public CompletedPaymentWindow(IEnumerable<string> recipients, DateOnly selectedDate)
    {
        Title = "Գրանցել կատարված վճարում"; Width = 490; Height = 410; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _date = new DatePicker { SelectedDate = selectedDate.ToDateTime(TimeOnly.MinValue), MinWidth = 280 };
        foreach (var recipient in recipients.Order()) _recipient.Items.Add(recipient);

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Label("Մատակարար կամ այլ ստացող")); panel.Children.Add(_recipient);
        panel.Children.Add(Label("Փաստացի վճարված գումար (֏)")); panel.Children.Add(_amount);
        panel.Children.Add(Label("Վճարման օր")); panel.Children.Add(_date);
        panel.Children.Add(Label("Նշում")); panel.Children.Add(_note);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել կատարված վճարումը", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += Save_Click; actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions);
        Content = panel;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_recipient.Text) || _date.SelectedDate is null ||
            !decimal.TryParse(_amount.Text.Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            MessageBox.Show("Լրացրեք ստացողը, գումարը և վճարման օրը։", "Չլրացված տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        Result = new CompletedPayment(_recipient.Text.Trim(), amount, DateOnly.FromDateTime(_date.SelectedDate.Value), _note.Text.Trim());
        DialogResult = true;
    }

    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };
}
