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

    public RequiredPaymentWindow(DateOnly selectedDate, RequiredPaymentTemplate? existing = null)
    {
        _selectedDate = selectedDate; _existing = existing;
        Title = existing is null ? "Ավելացնել պարտադիր վճարում" : "Խմբագրել պարտադիր վճարումը"; Width = 500; Height = 550; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var category in new[] { "Աշխատավարձ", "Վարձակալություն", "Հարկեր", "Կոմունալ", "Վարկ / պարտավորություն", "Այլ" }) _category.Items.Add(category);
        _category.Text = existing?.Category ?? "Աշխատավարձ";
        foreach (var day in Enumerable.Range(1, 31)) _day.Items.Add(day);
        _day.SelectedItem = existing?.PaymentDay ?? 1;
        _name.Text = existing?.Name ?? ""; _amount.Text = existing?.Amount.ToString("0") ?? ""; _note.Text = existing?.Note ?? "";
        _scope.Items.Add("Կրկնել ամեն ամիս");
        _scope.Items.Add("Միայն ընտրված ամսվա համար");
        _scope.SelectedIndex = existing is { RepeatsMonthly: false } ? 1 : 0;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Label("Կատեգորիա (օր.` Կոմունալ, Վարկ, Աշխատավարձ)")); panel.Children.Add(_category);
        panel.Children.Add(Label("Անվանում / ում է վճարվում (օր.` Էներգիա, ԱԿԲԱ, Լուսինե)")); panel.Children.Add(_name);
        panel.Children.Add(Label("Գումարի չափ (֏)")); panel.Children.Add(_amount);
        panel.Children.Add(Label("Ամսվա վճարման օր")); panel.Children.Add(_day);
        panel.Children.Add(Label("Կրկնման կարգ")); panel.Children.Add(_scope);
        panel.Children.Add(Label("Նշում")); panel.Children.Add(_note);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել գրաֆիկում", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += Save_Click; actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions);
        Content = panel;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_category.Text) || string.IsNullOrWhiteSpace(_name.Text) || _day.SelectedItem is not int day ||
            !decimal.TryParse(_amount.Text.Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        {
            MessageBox.Show("Լրացրեք կատեգորիան, անվանումը, գումարը և վճարման օրը։", "Չլրացված տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        var repeats = _scope.SelectedIndex == 0;
        DateOnly? onlyMonth = repeats ? null : new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);
        Result = new RequiredPaymentTemplate(_existing?.Id ?? Guid.NewGuid(), _category.Text.Trim(), _name.Text.Trim(), amount, day, _note.Text.Trim(), true, repeats, onlyMonth);
        DialogResult = true;
    }

    private static TextBlock Label(string value) => new() { Text = value, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };
}
