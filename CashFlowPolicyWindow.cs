using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PatarikAIOS;

/// <summary>Owner-facing financial planning settings.  Values are policies, not transactions.</summary>
public sealed class CashFlowPolicyWindow : Window
{
    private readonly TextBox _reserve;
    private readonly TextBox _weeklySales;
    private readonly ComboBox _allocation;
    private readonly CashFlowPolicy _source;

    public CashFlowPolicy? Result { get; private set; }

    public CashFlowPolicyWindow(CashFlowPolicy source)
    {
        _source = source;
        Title = "Ֆինանսական կանոններ";
        Width = 540; Height = 390;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = "Այս կարգավորումները սահմանում են շաբաթվա պլանավորման սահմանները։ Դրանք փաստացի վճարում չեն ստեղծում։",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14)
        });

        panel.Children.Add(Label("Նվազագույն պաշտպանական մնացորդ (֏)"));
        _reserve = new TextBox { Text = source.MinimumReserve.ToString("0", CultureInfo.InvariantCulture) };
        panel.Children.Add(_reserve);

        panel.Children.Add(Label("Շաբաթվա վաճառքի բազա (֏, ցանկության դեպքում)"));
        _weeklySales = new TextBox { Text = source.WeeklySalesBaselineOverride?.ToString("0", CultureInfo.InvariantCulture) ?? string.Empty };
        panel.Children.Add(_weeklySales);
        panel.Children.Add(new TextBlock
        {
            Text = "Դատարկ թողնելու դեպքում ծրագիրը հիմք է վերցնում նախորդ 7 օրվա ՀԾ վաճառքի փաստացի միջինը։",
            TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 4, 0, 12)
        });

        panel.Children.Add(Label("Հաստատուն ծախսի պահուստի հաշվարկ"));
        _allocation = new ComboBox { ItemsSource = new[] { "Մինչև վճարման օրը կուտակել", "Ամսվա օրերին հավասար բաժանել" }, SelectedIndex = source.FixedCostAllocation == FixedCostAllocationMode.EvenlyAcrossMonth ? 1 : 0 };
        panel.Children.Add(_allocation);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել", MinWidth = 90 };
        cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել", MinWidth = 105, Margin = new Thickness(8, 0, 0, 0), IsDefault = true };
        save.Click += (_, _) => Save();
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);
        Content = panel;
    }

    private void Save()
    {
        if (!TryAmount(_reserve.Text, out var reserve) || reserve < 0m)
        {
            MessageBox.Show("Նշեք ճիշտ պաշտպանական մնացորդ։", "Ֆինանսական կանոններ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        decimal? weeklySales = null;
        if (!string.IsNullOrWhiteSpace(_weeklySales.Text))
        {
            if (!TryAmount(_weeklySales.Text, out var value) || value <= 0m)
            {
                MessageBox.Show("Շաբաթվա վաճառքի բազան նշեք դրական գումարով կամ թողեք դատարկ։", "Ֆինանսական կանոններ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            weeklySales = value;
        }

        Result = _source with
        {
            MinimumReserve = reserve,
            WeeklySalesBaselineOverride = weeklySales,
            FixedCostAllocation = _allocation.SelectedIndex == 1 ? FixedCostAllocationMode.EvenlyAcrossMonth : FixedCostAllocationMode.ByDueDate
        };
        DialogResult = true;
    }

    private static Label Label(string text) => new() { Content = text, Margin = new Thickness(0, 8, 0, 4) };

    private static bool TryAmount(string text, out decimal value)
    {
        var cleaned = new string(text.Where(char.IsDigit).ToArray());
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
