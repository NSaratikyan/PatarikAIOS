using System.Globalization;

namespace PatarikAIOS;

public sealed class RequiredPaymentWindow : Window
{
    private readonly ComboBox _category = new() { IsEditable = true, MinWidth = 280 };
    private readonly TextBox _name = new() { MinWidth = 280 };
    private readonly TextBox _amount = new() { MinWidth = 280 };
    private readonly ComboBox _day = new() { MinWidth = 280 };
    private readonly TextBox _note = new() { MinWidth = 280, MinHeight = 55, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
    private readonly ComboBox _scope = new() { MinWidth = 280 };
    private readonly DateOnly _selectedDate;
    private readonly RequiredPaymentTemplate? _existing;
    public RequiredPaymentTemplate? Result { get; private set; }
    public event Action<RequiredPaymentTemplate>? PaymentSavedAndNew;

    public RequiredPaymentWindow(DateOnly selectedDate, RequiredPaymentTemplate? existing = null)
    {
        _selectedDate = selectedDate; _existing = existing;
        Title = existing is null ? "Պարտադիր վճարում ավելացնել" : "Պարտադիր վճարում խմբագրել";
        Width = 500; Height = 550; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var category in new[] { "Աշխատավարձ", "Կոմունալ", "Վարկ", "Հարկ", "Վարձավճար", "Այլ" }) _category.Items.Add(category);
        _category.Text = existing?.Category ?? "Աշխատավարձ";
        foreach (var day in Enumerable.Range(1, 31)) _day.Items.Add(day);
        _day.SelectedItem = existing?.PaymentDay ?? selectedDate.Day;
        _name.Text = existing?.Name ?? ""; _amount.Text = existing?.Amount.ToString("0") ?? ""; _note.Text = existing?.Note ?? "";
        _scope.Items.Add("Կրկնվում է ամեն ամիս");
        _scope.Items.Add("Միայն ընտրված ամսվա համար");
        _scope.SelectedIndex = existing is { RepeatsMonthly: false } ? 1 : 0;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Label("Կատեգորիա")); panel.Children.Add(_category);
        panel.Children.Add(Label("Անվանում / ում է վճարվում")); panel.Children.Add(_name);
        panel.Children.Add(Label("Գումար (֏)")); panel.Children.Add(_amount);
        panel.Children.Add(Label("Վճարման օր")); panel.Children.Add(_day);
        panel.Children.Add(Label("Կրկնություն")); panel.Children.Add(_scope);
        panel.Children.Add(Label("Նշում")); panel.Children.Add(_note);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true }; cancel.Click += (_, _) => Close();
        if (existing is null)
        {
            var saveAndNew = new Button { Content = "Պահպանել և նոր ավելացնել", Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
            saveAndNew.Click += (_, _) =>
            {
                var payment = TryBuildPayment();
                if (payment is null) return;
                PaymentSavedAndNew?.Invoke(payment);
                _name.Clear(); _amount.Clear(); _note.Clear(); _day.SelectedItem = _selectedDate.Day; _name.Focus();
            };
            actions.Children.Add(saveAndNew);
        }
        var save = new Button { Content = "Պահպանել", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += (_, _) => { var payment = TryBuildPayment(); if (payment is null) return; Result = payment; DialogResult = true; };
        actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions);
        Content = panel;
    }

    private RequiredPaymentTemplate? TryBuildPayment()
    {
        if (string.IsNullOrWhiteSpace(_category.Text) || string.IsNullOrWhiteSpace(_name.Text) || _day.SelectedItem is not int day ||
            !decimal.TryParse(_amount.Text.Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            MessageBox.Show("Լրացրեք կատեգորիան, անվանումը, գումարը և վճարման օրը։", "Սխալ տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning); return null;
        }
        var repeats = _scope.SelectedIndex == 0;
        DateOnly? onlyMonth = repeats ? null : new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);
        return new RequiredPaymentTemplate(_existing?.Id ?? Guid.NewGuid(), _category.Text.Trim(), _name.Text.Trim(), amount, day, _note.Text.Trim(), true, repeats, onlyMonth);
    }

    private static TextBlock Label(string value) => new() { Text = value, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };
}
