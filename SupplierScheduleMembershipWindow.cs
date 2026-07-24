namespace PatarikAIOS;

public sealed record SupplierScheduleMembershipDraft(string Supplier, DateOnly Date, bool IsIncluded, bool RecursInFuture, string Note);

public sealed class SupplierScheduleMembershipWindow : Window
{
    private readonly ComboBox _supplier = new() { MinWidth = 330, IsEditable = true };
    private readonly DatePicker _date;
    private readonly ComboBox _operation = new() { MinWidth = 330, SelectedIndex = 0 };
    private readonly ComboBox _scope = new() { MinWidth = 330, SelectedIndex = 0 };
    private readonly TextBox _note = new() { MinWidth = 330, MinHeight = 50, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    public SupplierScheduleMembershipDraft? Result { get; private set; }

    public SupplierScheduleMembershipWindow(IEnumerable<string> suppliers, DateOnly selectedDate)
    {
        Title = "Մատակարարի գրաֆիկի փոփոխություն"; Width = 520; Height = 455; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _date = new DatePicker { MinWidth = 330, SelectedDate = selectedDate.ToDateTime(TimeOnly.MinValue) };
        foreach (var supplier in suppliers.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)) _supplier.Items.Add(supplier);
        _operation.Items.Add("Ավելացնել մատակարարին այս օրվա գրաֆիկում");
        _operation.Items.Add("Հեռացնել մատակարարին այս օրվա գրաֆիկից");
        _scope.Items.Add("Միայն ընտրված օրվա / շաբաթվա համար");
        _scope.Items.Add("Ընտրված օրվանից սկսած՝ հետագա շաբաթների համար");
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Label("Մատակարար")); panel.Children.Add(_supplier);
        panel.Children.Add(Label("Օր")); panel.Children.Add(_date);
        panel.Children.Add(Label("Գործողություն")); panel.Children.Add(_operation);
        panel.Children.Add(Label("Կիրառման շրջանակ")); panel.Children.Add(_scope);
        panel.Children.Add(Label("Նշում (ըստ ցանկության)")); panel.Children.Add(_note);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել" }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել", Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += Save_Click;
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons); Content = panel;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var supplier = _supplier.Text.Trim();
        if (string.IsNullOrWhiteSpace(supplier) || _date.SelectedDate is null)
        {
            MessageBox.Show("Նշեք մատակարարին և օրը։", "Տվյալները լրացված չեն", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result = new SupplierScheduleMembershipDraft(supplier, DateOnly.FromDateTime(_date.SelectedDate.Value), _operation.SelectedIndex == 0, _scope.SelectedIndex == 1, _note.Text.Trim());
        DialogResult = true;
    }

    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 9, 0, 4) };
}
