using System.Globalization;

namespace PatarikAIOS;

public sealed record DeliveryPlanChangeDraft(DayOfWeek Weekday, string Supplier, decimal Amount, string Instruction);

public sealed class DeliveryPlanChangeWindow : Window
{
    private readonly ComboBox _supplier = new() { MinWidth = 300 };
    private readonly ComboBox _weekday = new() { MinWidth = 300 };
    private readonly TextBox _amount = new() { MinWidth = 300 };
    private readonly TextBox _instruction = new() { MinWidth = 300, MinHeight = 65, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
    public DeliveryPlanChangeDraft? Result { get; private set; }

    public DeliveryPlanChangeWindow(IReadOnlyList<SupplierDeliveryPattern> patterns)
    {
        Title = "Փոխել մատակարարման պլանը"; Width = 500; Height = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var item in patterns.OrderBy(x => x.Supplier).Select(x => x.Supplier).Distinct()) _supplier.Items.Add(item);
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>()) _weekday.Items.Add(new DayItem(day));
        _weekday.SelectedItem = _weekday.Items.Cast<DayItem>().First(x => x.Day == DayOfWeek.Monday);
        _instruction.Text = "Օրինակ՝ այս մատակարարի ավտոմատ գումարը փոխել նշված գումարով";
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Label("Մատակարար")); panel.Children.Add(_supplier);
        panel.Children.Add(Label("Մատակարարման օր")); panel.Children.Add(_weekday);
        panel.Children.Add(Label("Նոր պլանային գումար (֏)")); panel.Children.Add(_amount);
        panel.Children.Add(Label("Հրահանգ / փոփոխության պատճառ")); panel.Children.Add(_instruction);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել" }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել փոփոխությունը", Background = new SolidColorBrush(Color.FromRgb(15,118,110)), Foreground = Brushes.White };
        save.Click += Save_Click; buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons); Content = panel;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_supplier.SelectedItem is not string supplier || _weekday.SelectedItem is not DayItem day || !decimal.TryParse(_amount.Text.Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount < 0)
        { MessageBox.Show("Ընտրեք մատակարարն ու օրը, ապա նշեք գումարը։", "Չլրացված տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        Result = new DeliveryPlanChangeDraft(day.Day, supplier, amount, _instruction.Text.Trim()); DialogResult = true;
    }
    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 11, 0, 4) };
    private sealed record DayItem(DayOfWeek Day) { public override string ToString() => Views.ArmenianWeekdayLabel(Day); }
}
