using System.Globalization;

namespace PatarikAIOS;

public sealed record FundsTransactionEntry(
    DateOnly Date,
    string Source,
    string Kind,
    decimal Amount,
    string RecipientOrPurpose,
    string? Target,
    bool IsOldDebtPayment);

/// <summary>Simple owner form for a cash/bank movement.  It records one real
/// business movement, not two unrelated rows.</summary>
public sealed class FundsTransactionWindow : Window
{
    private readonly DatePicker _date;
    private readonly ComboBox _source = new() { ItemsSource = new[] { "0001", "0002", "bank" }, SelectedIndex = 0 };
    private readonly ComboBox _kind = new() { ItemsSource = new[] { "Մատակարարի վճարում", "Այլ ծախս", "Ներքին փոխանցում" }, SelectedIndex = 0 };
    private readonly TextBox _amount = new();
    private readonly ComboBox _recipient = new() { IsEditable = true };
    private readonly ComboBox _target = new() { ItemsSource = new[] { "0001", "0002", "bank" }, SelectedIndex = 1 };
    private readonly CheckBox _oldDebt = new() { Content = "Հին պարտքի վճարում" };
    public FundsTransactionEntry? Result { get; private set; }

    public FundsTransactionWindow(DateOnly selectedDate, IEnumerable<string> suppliers)
    {
        Title = "Գումարի շարժ գրանցել";
        Width = 510; Height = 530; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _date = new DatePicker { SelectedDate = selectedDate.ToDateTime(TimeOnly.MinValue) };
        foreach (var supplier in suppliers.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Order()) _recipient.Items.Add(supplier);
        _kind.SelectionChanged += (_, _) => RefreshFields();

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Գումարի շարժ", FontSize = 20, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Առաջինը գումարի ելքագրվող աղբյուրն է։ Ներքին փոխանցումը ծախս չէ և չի փոխում ընդհանուր հասանելի գումարը։", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 6, 0, 12) });
        panel.Children.Add(Label("Ամսաթիվ")); panel.Children.Add(_date);
        panel.Children.Add(Label("Որտեղի՞ց է ելքը")); panel.Children.Add(_source);
        panel.Children.Add(Label("Գործողության տեսակ")); panel.Children.Add(_kind);
        panel.Children.Add(Label("Գումար (֏)")); panel.Children.Add(_amount);
        panel.Children.Add(Label("Մատակարար / նպատակ")); panel.Children.Add(_recipient);
        panel.Children.Add(_oldDebt);
        panel.Children.Add(Label("Որտե՞ղ է մուտքը (միայն ներքին փոխանցման համար)")); panel.Children.Add(_target);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true }; cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += Save_Click;
        actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions);
        Content = panel;
        RefreshFields();
    }

    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 3) };

    private void RefreshFields()
    {
        var transfer = _kind.SelectedIndex == 2;
        _target.IsEnabled = transfer;
        _recipient.IsEnabled = !transfer;
        _oldDebt.IsEnabled = _kind.SelectedIndex == 0;
        if (transfer) _oldDebt.IsChecked = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_date.SelectedDate is null ||
            (!decimal.TryParse(_amount.Text.Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) &&
             !decimal.TryParse(_amount.Text.Replace(" ", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out amount)) || amount <= 0m)
        {
            MessageBox.Show("Լրացրեք ամսաթիվն ու դրական գումարը։", "Սխալ տվյալ", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        var kind = _kind.SelectedItem as string ?? string.Empty;
        var source = _source.SelectedItem as string ?? "0001";
        var target = _target.SelectedItem as string;
        var recipient = _recipient.Text.Trim();
        if (kind == "Ներքին փոխանցում")
        {
            if (string.IsNullOrWhiteSpace(target) || target == source)
            {
                MessageBox.Show("Ընտրեք այլ մուտքագրվող աղբյուր։", "Սխալ փոխանցում", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }
            recipient = string.Empty;
        }
        else if (string.IsNullOrWhiteSpace(recipient))
        {
            MessageBox.Show("Նշեք մատակարարին կամ ծախսի նպատակը։", "Սխալ տվյալ", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        Result = new FundsTransactionEntry(DateOnly.FromDateTime(_date.SelectedDate.Value), source, kind, amount, recipient,
            kind == "Ներքին փոխանցում" ? target : null, _oldDebt.IsChecked == true);
        DialogResult = true;
    }
}
