using System.Globalization;
using System.Windows.Media;

namespace PatarikAIOS;

public sealed record PendingOrderEditValues(string Supplier, decimal Order, decimal Payment, decimal OldDebtPayment);

public sealed class PendingOrderEditWindow : Window
{
    private readonly ComboBox _supplier;
    private readonly TextBox _order;
    private readonly TextBox _payment;
    private readonly TextBox _oldDebt;
    private readonly StackPanel _suggestions = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
    private readonly IReadOnlyList<string> _names;
    public PendingOrderEditValues? Result { get; private set; }

    public PendingOrderEditWindow(PendingEmployeeOrderChange change, IReadOnlyList<string> names)
    {
        _names = names;
        Title = "Հաստատման սպասող գրանցման խմբագրում";
        Width = 700; Height = 650; MinWidth = 580; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = $"{change.Date:dd.MM.yyyy} · {change.ReportedByName}", FontSize = 18, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Փոփոխեք անունը և փաստացի գումարները։ Պահպանումը դեռ չի հաստատում ստացումը կամ վճարումը։", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) });
        panel.Children.Add(Label("Մատակարար․ ընտրեք ցանկից կամ գրեք անունը"));
        _supplier = new ComboBox { IsEditable = true, IsTextSearchEnabled = false, ItemsSource = names.OrderBy(x => x).ToList(), Text = change.Supplier, Margin = new Thickness(0, 4, 0, 6) };
        panel.Children.Add(_supplier);
        panel.Children.Add(_suggestions);
        _supplier.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => RefreshSuggestions()));
        panel.Children.Add(Label($"Պլան՝ պատվեր {change.PlannedOrder:N0} ֏ / վճարում {change.PlannedPayment:N0} ֏ / հին պարտք {change.PlannedOldDebtPayment:N0} ֏"));
        _order = Amount(panel, "Փաստացի պատվեր / ստացում", change.ActualOrder);
        _payment = Amount(panel, "Փաստացի վճարում", change.ActualPayment);
        _oldDebt = Amount(panel, "Հին պարտքի փաստացի վճարում", change.ActualOldDebtPayment);
        panel.Children.Add(_error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true, Padding = new Thickness(10, 6, 10, 6) };
        var save = new Button { Content = "Պահպանել փոփոխությունները", IsDefault = true, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_supplier.Text)) { _error.Text = "Լրացրեք մատակարարի անունը։"; return; }
            if (!TryAmount(_order.Text, out var order) || !TryAmount(_payment.Text, out var payment) || !TryAmount(_oldDebt.Text, out var oldDebt))
            { _error.Text = "Գումարները պետք է լինեն ոչ բացասական թվեր։ Օրինակ՝ 10000 կամ 10000.50։"; return; }
            Result = new PendingOrderEditValues(_supplier.Text.Trim(), order, payment, oldDebt);
            DialogResult = true;
        };
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Loaded += (_, _) => RefreshSuggestions();
        RefreshSuggestions();
    }

    private void RefreshSuggestions()
    {
        _suggestions.Children.Clear();
        _suggestions.Children.Add(new TextBlock { Text = "Հավանական տարբերակներ (ընտրությունը ձերն է)", Foreground = Brushes.DimGray });
        var names = SupplierNameSuggestions.Find(_supplier.Text, _names);
        foreach (var name in names)
        {
            var button = new Button { Content = name, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 3, 0, 0), Padding = new Thickness(8, 3, 8, 3) };
            button.Click += (_, _) => _supplier.Text = name;
            _suggestions.Children.Add(button);
        }
        if (names.Count == 0) _suggestions.Children.Add(new TextBlock { Text = "Մոտ տարբերակ չի գտնվել․ կարող եք գրել անունը կամ ընտրել ամբողջ ցանկից։", TextWrapping = TextWrapping.Wrap });
    }

    private static TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 3) };
    private static TextBox Amount(Panel panel, string label, decimal value)
    {
        panel.Children.Add(Label(label));
        var input = new TextBox { Text = value.ToString("0.##", CultureInfo.InvariantCulture), Padding = new Thickness(6), MinWidth = 160 };
        panel.Children.Add(input); return input;
    }

    public static bool TryAmount(string text, out decimal amount) => decimal.TryParse(
        text.Trim().Replace(" ", "").Replace("\u00a0", "").Replace("\u202f", ""),
        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount) && amount >= 0m;
}
