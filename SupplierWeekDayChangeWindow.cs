namespace PatarikAIOS;

public sealed record SupplierWeekDayChangeDraft(SupplierWeekPlanRow Source, DateOnly NewDate, bool RecursInFuture, string Note);

public sealed class SupplierWeekDayChangeWindow : Window
{
    private readonly ComboBox _source = new() { MinWidth = 330 };
    private readonly DatePicker _newDate = new() { MinWidth = 330, SelectedDate = DateTime.Today };
    private readonly ComboBox _scope = new() { MinWidth = 330, SelectedIndex = 0 };
    private readonly TextBox _note = new() { MinWidth = 330, MinHeight = 55, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    public SupplierWeekDayChangeDraft? Result { get; private set; }

    public SupplierWeekDayChangeWindow(IReadOnlyList<SupplierWeekPlanRow> rows)
    {
        Title = "Փոխել մատակարարի օրը"; Width = 520; Height = 460; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var row in rows.OrderBy(x => x.Date).ThenBy(x => x.Supplier)) _source.Items.Add(new SourceItem(row));
        if (_source.Items.Count > 0) _source.SelectedIndex = 0;
        _scope.Items.Add("Միայն այս շաբաթվա համար"); _scope.Items.Add("Հետագայում բոլոր շաբաթների համար");
        _note.Text = "Օրինակ՝ մատակարարը պայմանավորվել է փոխել մատակարարման օրը";
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Label("Մատակարար և ներկայիս օր")); panel.Children.Add(_source);
        panel.Children.Add(Label("Նոր օր / ամսաթիվ")); panel.Children.Add(_newDate);
        panel.Children.Add(Label("Փոփոխության տարածում")); panel.Children.Add(_scope);
        panel.Children.Add(Label("Նշում")); panel.Children.Add(_note);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել" }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել փոփոխությունը", Background = new SolidColorBrush(Color.FromRgb(15,118,110)), Foreground = Brushes.White };
        save.Click += Save_Click; buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons); Content = panel;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_source.SelectedItem is not SourceItem source || _newDate.SelectedDate is null) { MessageBox.Show("Ընտրեք մատակարարն ու նոր օրը։", "Չլրացված տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        Result = new SupplierWeekDayChangeDraft(source.Row, DateOnly.FromDateTime(_newDate.SelectedDate.Value), _scope.SelectedIndex == 1, _note.Text.Trim());
        DialogResult = true;
    }
    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };
    private sealed record SourceItem(SupplierWeekPlanRow Row) { public override string ToString() => $"{Row.Supplier} — {Views.ArmenianWeekdayLabel(Row.Date.DayOfWeek)} ({Row.Date:dd.MM})"; }
}
