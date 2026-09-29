using System.Globalization;

namespace PatarikAIOS;

/// <summary>Auditable correction screen for one employee. Every displayed amount is a stored factual entry.</summary>
public sealed class SalaryEmployeeDetailsWindow : Window
{
    private string _employee;
    private readonly DateOnly _weekStart;
    private readonly List<SalaryAccrual> _accruals;
    private readonly List<SalaryPayment> _payments;
    private readonly Action _save;
    private readonly StackPanel _root = new() { Margin = new Thickness(22) };

    public SalaryEmployeeDetailsWindow(string employee, DateOnly weekStart, List<SalaryAccrual> accruals, List<SalaryPayment> payments, Action save)
    {
        _employee = employee; _weekStart = weekStart; _accruals = accruals; _payments = payments; _save = save;
        Title = $"Աշխատող — {employee}"; Width = 850; Height = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = new ScrollViewer { Content = _root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Render();
    }

    private void Render()
    {
        _root.Children.Clear();
        var accrued = SalaryRules.AccruedForWeek(_accruals, _weekStart, _employee);
        var paid = SalaryRules.PaidForWeek(_payments, _weekStart, _employee);
        _root.Children.Add(new TextBlock { Text = _employee, FontSize = 22, FontWeight = FontWeights.Bold });
        var name = new TextBox { Text = _employee, Width = 270 };
        var rename = new Button { Content = "Խմբագրել անունը", Margin = new Thickness(8,0,0,0) };
        rename.Click += (_,_) =>
        {
            var corrected=name.Text.Trim(); if(corrected.Length==0 || corrected==_employee) return;
            if(MessageBox.Show($"«{_employee}» անունով բոլոր աշխատավարձերն ու վճարումները գրանցե՞լ «{corrected}» անունով։ Եթե այդ անունն արդեն կա, տվյալները կմիավորվեն։","Անվան ուղղում",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
            for(var i=0;i<_accruals.Count;i++) if(string.Equals(_accruals[i].Employee,_employee,StringComparison.OrdinalIgnoreCase)) _accruals[i]=_accruals[i] with { Employee=corrected };
            for(var i=0;i<_payments.Count;i++) if(string.Equals(_payments[i].Employee,_employee,StringComparison.OrdinalIgnoreCase)) _payments[i]=_payments[i] with { Employee=corrected };
            _save(); _employee=corrected; Render();
        };
        _root.Children.Add(new StackPanel { Orientation=Orientation.Horizontal, Children={name,rename}, Margin=new Thickness(0,8,0,8) });
        _root.Children.Add(new TextBlock { Text = $"Շաբաթ՝ {_weekStart:dd.MM.yyyy}–{_weekStart.AddDays(6):dd.MM.yyyy}   |   Գեներացված՝ {accrued:N0} ֏   |   Վճարված՝ {paid:N0} ֏   |   Մնացորդ՝ {accrued - paid:N0} ֏", Margin = new Thickness(0, 5, 0, 16) });
        _root.Children.Add(TitleText("Օրական աշխատավարձի գրառումներ"));
        foreach (var row in _accruals.Where(x => string.Equals(x.Employee, _employee, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Date).ToList())
            _root.Children.Add(AccrualRow(row));
        _root.Children.Add(TitleText("Աշխատավարձի վճարումներ"));
        foreach (var row in _payments.Where(x => string.Equals(x.Employee, _employee, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.PaidDate).ToList())
            _root.Children.Add(PaymentRow(row));
    }

    private UIElement AccrualRow(SalaryAccrual item)
    {
        var panel = RowPanel();
        var date = new DatePicker { SelectedDate = item.Date.ToDateTime(TimeOnly.MinValue), Width = 125 };
        var amount = new TextBox { Text = item.Amount.ToString("0"), Width = 110, Margin = new Thickness(8, 0, 0, 0) };
        var note = new TextBox { Text = item.Note, Width = 300, Margin = new Thickness(8, 0, 0, 0) };
        var save = new Button { Content = "Պահպանել", Margin = new Thickness(8, 0, 0, 0) };
        save.Click += (_, _) =>
        {
            if (date.SelectedDate is null || !TryAmount(amount.Text, out var value)) { Error(); return; }
            var index = _accruals.FindIndex(x => x.Id == item.Id);
            if (index >= 0) _accruals[index] = item with { Date = DateOnly.FromDateTime(date.SelectedDate.Value), Amount = value, Note = note.Text.Trim() };
            _save(); Render();
        };
        var remove = new Button { Content = "Հեռացնել", Margin = new Thickness(6, 0, 0, 0) };
        remove.Click += (_, _) => { if(MessageBox.Show("Հեռացնե՞լ այս աշխատավարձի գրառումը։ Պահուստային պատճենը կպահպանվի։","Հաստատում",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return; _accruals.RemoveAll(x => x.Id == item.Id); _save(); Render(); };
        panel.Children.Add(date); panel.Children.Add(amount); panel.Children.Add(note); panel.Children.Add(save); panel.Children.Add(remove); return panel;
    }

    private UIElement PaymentRow(SalaryPayment item)
    {
        var panel = RowPanel();
        var source=PaymentSourcePicker.Create(item.CashSource);
        var date = new DatePicker { SelectedDate = item.PaidDate.ToDateTime(TimeOnly.MinValue), Width = 125 };
        var amount = new TextBox { Text = item.Amount.ToString("0"), Width = 110, Margin = new Thickness(8, 0, 0, 0) };
        var note = new TextBox { Text = item.Note, Width = 300, Margin = new Thickness(8, 0, 0, 0) };
        var save = new Button { Content = "Պահպանել", Margin = new Thickness(8, 0, 0, 0) };
        save.Click += (_, _) =>
        {
            if (date.SelectedDate is null || !TryAmount(amount.Text, out var value)) { Error(); return; }
            var index = _payments.FindIndex(x => x.Id == item.Id);
            if (index >= 0) _payments[index] = item with { PaidDate = DateOnly.FromDateTime(date.SelectedDate.Value), Amount = value, Note = note.Text.Trim(), CashSource=PaymentSourcePicker.Value(source) };
            _save(); Render();
        };
        var remove = new Button { Content = "Հեռացնել", Margin = new Thickness(6, 0, 0, 0) };
        remove.Click += (_, _) => { if(MessageBox.Show("Հեռացնե՞լ այս վճարման գրառումը։ Պահուստային պատճենը կպահպանվի։","Հաստատում",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return; _payments.RemoveAll(x => x.Id == item.Id); _save(); Render(); };
        panel.Children.Add(new TextBlock { Text = $"Շաբաթ {item.WeekStart:dd.MM}", Width = 105, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(source);
        panel.Children.Add(date); panel.Children.Add(amount); panel.Children.Add(note); panel.Children.Add(save); panel.Children.Add(remove); return panel;
    }

    private static WrapPanel RowPanel() => new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
    private static TextBlock TitleText(string value) => new() { Text = value, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 6) };
    private static bool TryAmount(string value, out decimal amount) => decimal.TryParse(value.Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out amount) && amount >= 0;
    private static void Error() => MessageBox.Show("Ստուգեք ամսաթիվն ու գումարը։", "Սխալ տվյալներ", MessageBoxButton.OK, MessageBoxImage.Warning);
}
