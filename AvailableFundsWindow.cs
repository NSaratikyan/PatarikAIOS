using System.Globalization;

namespace PatarikAIOS;

public sealed class AvailableFundsWindow : Window
{
    private readonly TextBox _vault;
    private readonly TextBox _cashDesk;
    private readonly TextBox _bankReport;
    private readonly TextBox _ameria;
    private readonly TextBox _idram;
    private readonly DatePicker _openingMonth;
    public AvailableFundsSettings? Result { get; private set; }

    public AvailableFundsWindow(AvailableFundsBreakdown current, DateOnly selectedDate)
    {
        Title = "Հասանելի միջոցների մանրամասներ";
        Width = 520; Height = 545;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        _vault = AmountBox(current.CashVault);
        _cashDesk = AmountBox(current.CashDesk);
        _bankReport = AmountBox(current.BankReport);
        _ameria = AmountBox(current.AmeriabankPos099);
        _idram = AmountBox(current.Idram);
        _openingMonth = new DatePicker { SelectedDate = new DateOnly(selectedDate.Year, selectedDate.Month, 1).ToDateTime(TimeOnly.MinValue) };

        var content = new StackPanel { Margin = new Thickness(24) };
        content.Children.Add(new TextBlock { Text = "Հասանելի միջոցներ", FontSize = 20, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock
        {
            Text = "Կանխիկը = Պահոց (0002) + Դրամարկղ (0001)։ Բանկը = բանկային հաշվետվություն + Ամերիաբանկ POS (099) + Idram։",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 10)
        });
        content.Children.Add(Label("Բացման մնացորդի ամիս")); content.Children.Add(_openingMonth);
        content.Children.Add(Label("Պահոց — 0002")); content.Children.Add(_vault);
        content.Children.Add(Label("Դրամարկղ — 0001")); content.Children.Add(_cashDesk);
        content.Children.Add(Label("Բանկային մնացորդ՝ հաշվետվությունից")); content.Children.Add(_bankReport);
        content.Children.Add(Label("Ամերիաբանկ POS գործընկեր — 099")); content.Children.Add(_ameria);
        content.Children.Add(Label("Idram")); content.Children.Add(_idram);

        var save = new Button { Content = "Պահպանել", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += Save_Click;
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true };
        cancel.Click += (_, _) => Close();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        actions.Children.Add(cancel); actions.Children.Add(save); content.Children.Add(actions);
        Content = content;
    }

    private static TextBox AmountBox(decimal value) => new() { Text = value.ToString("0.##", CultureInfo.InvariantCulture) };
    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 9, 0, 3) };

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryAmount(_vault, out var vault) || !TryAmount(_cashDesk, out var cashDesk) ||
            !TryAmount(_bankReport, out var bankReport) || !TryAmount(_ameria, out var ameria) || !TryAmount(_idram, out var idram))
        {
            MessageBox.Show("Գումարների դաշտերում գրեք միայն թիվ։", "Սխալ տվյալ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var opening = _openingMonth.SelectedDate is null ? (DateOnly?)null : DateOnly.FromDateTime(_openingMonth.SelectedDate.Value);
        Result = new AvailableFundsSettings(vault, cashDesk, bankReport, ameria, idram, opening);
        DialogResult = true;
    }

    private static bool TryAmount(TextBox box, out decimal value) =>
        decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
        decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
