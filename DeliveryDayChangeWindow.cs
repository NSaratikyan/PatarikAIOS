namespace PatarikAIOS;

public sealed record DeliveryDayChangeDraft(string Supplier, DayOfWeek CurrentDay, DayOfWeek NewDay, bool IsOneTime, DateOnly OneTimeDate, string Instruction);

public sealed class DeliveryDayChangeWindow : Window
{
    private readonly IReadOnlyList<SupplierDeliveryPattern> _patterns;
    private readonly ComboBox _supplier = new() { MinWidth = 300 };
    private readonly ComboBox _currentDay = new() { MinWidth = 300 };
    private readonly ComboBox _newDay = new() { MinWidth = 300 };
    private readonly ComboBox _changeType = new() { MinWidth = 300, SelectedIndex = 1 };
    private readonly DatePicker _oneTimeDate = new() { MinWidth = 300, SelectedDate = DateTime.Today };
    private readonly TextBox _instruction = new() { MinWidth = 300, MinHeight = 55, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
    public DeliveryDayChangeDraft? Result { get; private set; }

    public DeliveryDayChangeWindow(IReadOnlyList<SupplierDeliveryPattern> patterns)
    {
        _patterns = patterns; Title = "Փոխել մատակարարի մատակարարման օրը"; Width = 520; Height = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var supplier in patterns.Where(x => !x.IsOneTime).Select(x => x.Supplier).Distinct().Order()) _supplier.Items.Add(supplier);
        _supplier.SelectionChanged += (_, _) => LoadCurrentDays();
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>()) _newDay.Items.Add(new DayItem(day));
        _changeType.Items.Add("Միայն այս շաբաթվա համար"); _changeType.Items.Add("Հետագայում միշտ այս օրն է գալու");
        _instruction.Text = "Օրինակ՝ մատակարարը պայմանավորվել է փոխել մատակարարման օրը";
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Label("Մատակարար")); panel.Children.Add(_supplier);
        panel.Children.Add(Label("Ներկա ավտոմատ օր")); panel.Children.Add(_currentDay);
        panel.Children.Add(Label("Նոր մատակարարման օր")); panel.Children.Add(_newDay);
        panel.Children.Add(Label("Փոփոխության տեսակը")); panel.Children.Add(_changeType);
        panel.Children.Add(Label("Այս շաբաթվա ստացման օր")); panel.Children.Add(_oneTimeDate);
        panel.Children.Add(Label("Հրահանգ / նշում")); panel.Children.Add(_instruction);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել" }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել օրը", Background = new SolidColorBrush(Color.FromRgb(15,118,110)), Foreground = Brushes.White };
        save.Click += Save_Click; buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons); Content = panel;
    }
    private void LoadCurrentDays()
    {
        _currentDay.Items.Clear();
        if (_supplier.SelectedItem is not string supplier) return;
        foreach (var day in _patterns.Where(x => x.Supplier == supplier && !x.IsOneTime).Select(x => x.Weekday).Distinct().OrderBy(DayOrder)) _currentDay.Items.Add(new DayItem(day));
        if (_currentDay.Items.Count > 0) _currentDay.SelectedIndex = 0;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_supplier.SelectedItem is not string supplier || _currentDay.SelectedItem is not DayItem current || _newDay.SelectedItem is not DayItem next || _oneTimeDate.SelectedDate is null)
        { MessageBox.Show("Լրացրեք մատակարարն ու օրերը։", "Չլրացված տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        Result = new DeliveryDayChangeDraft(supplier, current.Day, next.Day, _changeType.SelectedIndex == 0, DateOnly.FromDateTime(_oneTimeDate.SelectedDate.Value), _instruction.Text.Trim());
        DialogResult = true;
    }
    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };
    private static int DayOrder(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;
    private sealed record DayItem(DayOfWeek Day) { public override string ToString() => Views.ArmenianWeekdayLabel(Day); }
}
