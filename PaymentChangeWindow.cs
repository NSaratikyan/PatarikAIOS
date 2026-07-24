using System.Globalization;

namespace PatarikAIOS;

public sealed record PaymentChangeDraft(string Supplier, decimal Amount, DateOnly PlannedDate, bool IsMandatory, bool IsOldDebtPayment, string Reason);

/// <summary>Owner-entered payment agreement. It changes the in-app plan only; no payment is sent to HԾ or a bank.</summary>
public sealed class PaymentChangeWindow : Window
{
    private readonly ComboBox _supplier = new() { IsEditable = true, MinWidth = 280 };
    private readonly TextBox _amount = new() { MinWidth = 280 };
    private readonly DatePicker _plannedDate = new() { MinWidth = 280, SelectedDate = DateTime.Today };
    private readonly ComboBox _paymentType = new() { MinWidth = 280, SelectedIndex = 0 };
    private readonly CheckBox _mandatory = new() { Content = "Պարտադիր վճարում", IsChecked = true };
    private readonly TextBox _reason = new() { MinWidth = 280, MinHeight = 70, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
    public PaymentChangeDraft? Result { get; private set; }

    public PaymentChangeWindow(IEnumerable<string> suppliers)
    {
        Title = "Վճարման փոփոխություն կամ պայմանավորվածություն";
        // Keep the action buttons visible even at Windows display scaling above 100%.
        Width = 500; Height = 560; MinHeight = 520; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResize;
        foreach (var supplier in suppliers.Order()) _supplier.Items.Add(supplier);
        _paymentType.Items.Add("Հին պարտքի վճարում");
        _paymentType.Items.Add("Պատվերի վճարում");
        _reason.Text = "Օրինակ՝ այսօր չի վճարվել, պայմանավորվել ենք վճարել նշված օրը";

        var content = new StackPanel { Margin = new Thickness(24) };
        content.Children.Add(Label("Մատակարար")); content.Children.Add(_supplier);
        content.Children.Add(Label("Գումար (֏)")); content.Children.Add(_amount);
        content.Children.Add(Label("Վճարման նոր / համաձայնեցված օր")); content.Children.Add(_plannedDate);
        content.Children.Add(Label("Վճարման տեսակ")); content.Children.Add(_paymentType);
        content.Children.Add(_mandatory);
        content.Children.Add(Label("Հրահանգ կամ պայմանավորվածություն")); content.Children.Add(_reason);
        content.Children.Add(new TextBlock { Text = "Ավելացնելուց հետո տվյալը ավտոմատ պահվում է այս համակարգչում։ Բանկային կամ ՀԾ վճարում չի ստեղծվում։", Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 6) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել և ավելացնել գրաֆիկ", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15,118,110)), Foreground = Brushes.White };
        save.Click += Save_Click; buttons.Children.Add(cancel); buttons.Children.Add(save); content.Children.Add(buttons);
        Content = content;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var supplier = _supplier.Text.Trim();
        var number = _amount.Text.Replace(" ", "");
        if (string.IsNullOrWhiteSpace(supplier) || !decimal.TryParse(number, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0 || _plannedDate.SelectedDate is null)
        {
            MessageBox.Show("Լրացրեք մատակարարի անունը, դրական գումարը և վճարման օրը։", "Չլրացված տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var date = DateOnly.FromDateTime(_plannedDate.SelectedDate.Value);
        Result = new PaymentChangeDraft(supplier, amount, date, _mandatory.IsChecked == true, _paymentType.SelectedIndex == 0, _reason.Text.Trim());
        DialogResult = true;
    }

    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) };
}
