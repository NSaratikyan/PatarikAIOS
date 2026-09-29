using System.Globalization;

namespace PatarikAIOS;

public sealed class SalaryPaymentWindow : Window
{
    private readonly ComboBox _employee = new() { IsEditable = true, MinWidth = 280 };
    private readonly TextBox _amount = new() { MinWidth = 280 };
    private readonly DatePicker _paidDate;
    private readonly TextBox _note = new() { MinWidth = 280, MinHeight = 55, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
    private readonly DateOnly _weekStart;
    public SalaryPayment? Result { get; private set; }
    private readonly ComboBox _source=PaymentSourcePicker.Create("0001");

    public SalaryPaymentWindow(DateOnly selectedDate, IEnumerable<string> employees)
    {
        _weekStart = SalaryRules.WeekStart(selectedDate);
        Title = "Աշխատավարձի վճարման գրանցում"; Width = 550; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _paidDate = new DatePicker { SelectedDate = selectedDate.ToDateTime(TimeOnly.MinValue), MinWidth = 280 };
        foreach (var employee in employees.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)) _employee.Items.Add(employee);
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Label($"Աշխատող (շաբաթ՝ {_weekStart:dd.MM}–{_weekStart.AddDays(6):dd.MM})")); panel.Children.Add(_employee);
        panel.Children.Add(Label("Վճարված գումար (֏)")); panel.Children.Add(_amount);
        panel.Children.Add(Label("Վճարման օր")); panel.Children.Add(_paidDate);
        panel.Children.Add(Label("Վճարման աղբյուր (արդեն ներմուծված է՝ ընտրեք առաջինը)")); panel.Children.Add(_source);
        panel.Children.Add(Label("Նշում")); panel.Children.Add(_note);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(22, 101, 52)), Foreground = Brushes.White };
        save.Click += Save_Click; actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions); Content = panel;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_employee.Text) || _paidDate.SelectedDate is null ||
            !decimal.TryParse(_amount.Text.Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0m)
        {
            MessageBox.Show("Լրացրեք աշխատողին, գումարը և վճարման օրը։", "Սխալ տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        Result = new SalaryPayment(Guid.NewGuid(), _weekStart, _employee.Text.Trim(), amount, DateOnly.FromDateTime(_paidDate.SelectedDate.Value), _note.Text.Trim(), PaymentSourcePicker.Value(_source));
        DialogResult = true;
    }
    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };
}
