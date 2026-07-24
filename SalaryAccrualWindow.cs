using System.Globalization;

namespace PatarikAIOS;

public sealed class SalaryAccrualWindow : Window
{
    private readonly ComboBox _employee = new() { IsEditable = true, MinWidth = 280 };
    private readonly TextBox _amount = new() { MinWidth = 280 };
    private readonly DatePicker _date;
    private readonly TextBox _note = new() { MinWidth = 280, MinHeight = 55, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
    public SalaryAccrual? Result { get; private set; }

    public SalaryAccrualWindow(DateOnly date, IEnumerable<string> employees)
    {
        Title = "Օրական աշխատավարձի գրանցում"; Width = 480; Height = 390; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _date = new DatePicker { SelectedDate = date.ToDateTime(TimeOnly.MinValue), MinWidth = 280 };
        foreach (var employee in employees.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)) _employee.Items.Add(employee);
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Label("Աշխատող")); panel.Children.Add(_employee);
        panel.Children.Add(Label("Այս օրվա աշխատավարձը (֏)")); panel.Children.Add(_amount);
        panel.Children.Add(Label("Ամսաթիվ")); panel.Children.Add(_date);
        panel.Children.Add(Label("Նշում / հերթափոխ")); panel.Children.Add(_note);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += Save_Click; actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions); Content = panel;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_employee.Text) || _date.SelectedDate is null ||
            !decimal.TryParse(_amount.Text.Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0m)
        {
            MessageBox.Show("Լրացրեք աշխատողին, գումարը և ամսաթիվը։", "Սխալ տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        Result = new SalaryAccrual(Guid.NewGuid(), DateOnly.FromDateTime(_date.SelectedDate.Value), _employee.Text.Trim(), amount, _note.Text.Trim(), DateTime.Now);
        DialogResult = true;
    }
    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };
}
