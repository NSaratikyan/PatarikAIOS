using System.Globalization;

namespace PatarikAIOS;

public sealed class CashDeskAdjustmentWindow : Window
{
    private readonly ComboBox _cashDesk;
    private readonly DatePicker _date;
    private readonly TextBox _amount;
    private readonly TextBox _note;
    public CashDeskAdjustment? Result { get; private set; }

    public CashDeskAdjustmentWindow(DateOnly selectedDate, IEnumerable<CashDeskAdjustment>? history = null)
    {
        Title = "Դրամարկղի / բանկի մնացորդի ուղղում";
        Width = 620; Height = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _cashDesk = new ComboBox { ItemsSource = new[] { "0001 — Դրամարկղ", "0002 — Պահոց", "bank — Ընդհանուր բանկային մնացորդ" }, SelectedIndex = 0 };
        _date = new DatePicker { SelectedDate = selectedDate.ToDateTime(TimeOnly.MinValue) };
        _amount = new TextBox();
        _note = new TextBox { Height = 74, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Ձեռքով ուղղում", FontSize = 20, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Գրեք ընտրված օրվա վերջի փաստացի մնացորդը։ Հաջորդ օրերը կհաշվարկվեն այս ուղղված մնացորդից։", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 12) });
        panel.Children.Add(Label("Դրամարկղ")); panel.Children.Add(_cashDesk);
        panel.Children.Add(Label("Ամսաթիվ")); panel.Children.Add(_date);
        panel.Children.Add(Label("Օրվա վերջի մնացորդ")); panel.Children.Add(_amount);
        panel.Children.Add(Label("Նշում")); panel.Children.Add(_note);
        var save = new Button { Content = "Պահպանել", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += Save_Click;
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true }; cancel.Click += (_, _) => Close();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions);
        panel.Children.Add(Label("Ուղղումների պատմություն (վերջին 30-ը)"));
        foreach (var entry in (history ?? []).Reverse().Take(30))
            panel.Children.Add(new TextBlock { Text = $"{entry.Date:dd.MM.yyyy} · {entry.CashDesk} · {entry.ClosingBalance:N2} ֏\n{entry.Note} · {entry.ChangedAt:dd.MM.yyyy HH:mm}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) });
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 3) };
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_date.SelectedDate is null || !decimal.TryParse(_amount.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) && !decimal.TryParse(_amount.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            MessageBox.Show("Նշեք ամսաթիվ և գումար։", "Սխալ տվյալ", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (string.IsNullOrWhiteSpace(_note.Text)) { MessageBox.Show("Նշեք ուղղման պատճառը։"); return; }
        Result = new CashDeskAdjustment(DateOnly.FromDateTime(_date.SelectedDate.Value), (_cashDesk.SelectedItem as string ?? "0001")[..4], amount, _note.Text.Trim(), DateTime.Now);
        DialogResult = true;
    }
}
