namespace PatarikAIOS;

public partial class MainWindow
{
    private async void EditSupplierDebt_Click(object sender, RoutedEventArgs e)
    {
        var names = KnownSupplierNames().OrderBy(x => x).ToList();
        var supplier = new ComboBox { ItemsSource = names, IsEditable = true, Margin = new Thickness(0, 10, 0, 10) };
        var amount = new TextBox { Margin = new Thickness(0, 10, 0, 10) };
        var info = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = "Պահպանել պարտքի մնացորդը", IsDefault = true };
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true };
        var window = new Window { Owner = this, Title = "Մատակարարի պարտքի ուղղում", Width = 580, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        SupplierWeekPlanRow? selected = null;
        supplier.SelectionChanged += (_, _) =>
        {
            var name = supplier.SelectedItem as string;
            if (name is null) return;
            selected = PlanForSelectedDate().FirstOrDefault(x => string.Equals(x.Supplier, name, StringComparison.OrdinalIgnoreCase))
                ?? new SupplierWeekPlanRow(_selectedDate, name, 0, 0, 0, DebtBeforeDate(name, _selectedDate));
            amount.Text = selected.Debt.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        };
        decimal debt = 0;
        save.Click += (_, _) =>
        {
            if (selected is null || !string.Equals(supplier.Text, selected.Supplier, StringComparison.OrdinalIgnoreCase))
            { info.Text = "Ընտրեք մատակարարին ցանկից։"; return; }
            if (!PendingOrderEditWindow.TryAmount(amount.Text, out debt))
            { info.Text = "Գրեք ոչ բացասական գումար։"; return; }
            window.DialogResult = true;
        };
        window.Content = new StackPanel { Margin = new Thickness(24), Children =
        {
            new TextBlock { Text = $"Օր՝ {_selectedDate:dd.MM.yyyy}", FontSize = 20 },
            new TextBlock { Text = "Գրեք այս օրվա վերջի պարտքը՝ արդեն ներառած այդ օրվա ստացումներն ու վճարումները։ Պատվերի և վճարման թվերը չեն փոխվի։ Սկզբնական պարտքի համար ընտրեք նախորդ օրը։", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,10,0,10) },
            supplier, new TextBlock { Text = "Պարտքի մնացորդ, դրամ" }, amount, info, save, cancel
        }};
        if (window.ShowDialog() != true || selected is null) return;
        await SaveAllSupplierRowsAsync([new SupplierRowEdit(selected, selected.OrderAmount, selected.PaymentAmount, selected.OldDebtPayment, debt)]);
    }
}
