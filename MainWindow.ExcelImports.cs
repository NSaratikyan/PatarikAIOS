namespace PatarikAIOS;

public partial class MainWindow
{
    private readonly ExcelImportStore _excelImportStore = new();

    private void OpenExcelImports_Click(object sender, RoutedEventArgs e)
    {
        var state = _excelImportStore.Load();
        var panel = new StackPanel { Margin = new Thickness(28) };
        var window = new Window { Owner = this, Title = "Excel ներմուծում և ձեռքով անկանխիկ վաճառք", Width = 1040, Height = 800, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        panel.Children.Add(new TextBlock { Text = "Excel ներմուծում", FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) });
        panel.Children.Add(new TextBlock { Text = "Ընտրել ֆայլերը   →   Ստուգել   →   Նախադիտել   →   Հաստատել", Foreground = PresentationTheme.Blue, Margin = new Thickness(0,0,0,20) });
        var files = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0,0,0,16) };
        foreach (var item in new[] { ("sales", "Վաճառքներ", "SalesAnalysis.xlsx"), ("cash", "Դրամարկղային շարժ", "CashDocument.xlsx"), ("warehouse", "Ստացումներ", "WarehouseDocument.xlsx") })
        {
            var batches = state.Batches.Where(b => b.Kind == item.Item1).ToList();
            var body = new StackPanel(); body.Children.Add(new TextBlock { Text = item.Item2, FontSize = 17, FontWeight = FontWeights.SemiBold });
            body.Children.Add(new TextBlock { Text = item.Item3, Foreground = System.Windows.Media.Brushes.SlateGray, Margin = new Thickness(0,10,0,10) });
            body.Children.Add(new TextBlock { Text = batches.Count == 0 ? "Դեռ չի ներմուծվել" : $"Պահպանված փաթեթներ՝ {batches.Count}", Foreground = PresentationTheme.Blue });
            files.Children.Add(new Border { Background = System.Windows.Media.Brushes.White, CornerRadius = new CornerRadius(10), BorderBrush = System.Windows.Media.Brushes.LightSteelBlue, BorderThickness = new Thickness(1), Padding = new Thickness(18), Margin = new Thickness(0,0,12,0), Child = body });
        }
        panel.Children.Add(files);
        panel.Children.Add(new TextBlock { Text = "Ընտրեք ամբողջական օրական հաշվետվությունները։ Նույն օրվա նույն տեսակի նախորդ ներմուծումը կփոխարինվի։ Excel ռեժիմում API-ի վաճառքները չեն գումարվում ֆայլերի վաճառքներին։", TextWrapping = TextWrapping.Wrap });
        var import = new Button { Content = "Ներմուծել Excel ֆայլեր", Background = PresentationTheme.Blue, Foreground = System.Windows.Media.Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,16,0,16) };
        import.Click += async (_, _) =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Multiselect = true };
            if (picker.ShowDialog() != true) return;
            try
            {
                var batches = picker.FileNames.Select(ExcelReportReader.Read).ToList();
                if (batches.SelectMany(b => b.Dates.Select(d => (b.Kind,d))).GroupBy(x => x).Any(g => g.Count() > 1))
                    throw new InvalidOperationException("Միաժամանակ ընտրված ֆայլերն ունեն նույն տեսակի նույն օրեր։ Ընտրեք օրվա մեկ ամբողջական հաշվետվություն։");
                var preview = string.Join("\n\n", batches.Select(b =>
                    $"{b.FileName} · {b.Dates.Min():dd.MM.yyyy}–{b.Dates.Max():dd.MM.yyyy}\nՎաճառքի տողեր՝ {b.Sales.Count}, ստացումներ՝ {b.Receipts.Count}, դրամական փաստաթղթեր՝ {b.Cash.Count}\n" + string.Join("\n",b.Warnings)));
                var fresh = _excelImportStore.Load();
                foreach (var batch in batches) ExcelImportStore.Merge(fresh,batch);
                var conflicts = ExcelReportReader.Reconcile(fresh);
                if (conflicts.Count > 0) throw new InvalidOperationException(string.Join("\n",conflicts) + "\nԸնտրեք նույն օրվա համադրելի ամբողջական ֆայլերը։");
                if (!ConfirmImportPreview(preview)) return;
                _excelImportStore.Save(fresh);
                ConfigureDataProvider(); _snapshot = null;
                _selectedDate = batches.SelectMany(x => x.Dates).Max();
                ViewDatePicker.SelectedDate = _selectedDate.ToDateTime(TimeOnly.MinValue);
                window.Close(); await LoadAsync("Dashboard");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message,"Ներմուծումը չի կատարվել"); }
        };
        panel.Children.Add(import);
        var mode = new Button { Content = state.Enabled ? "Անցնել API ռեժիմի" : "Օգտագործել պահպանված Excel տվյալները" };
        mode.Click += async (_,_) => { var fresh = _excelImportStore.Load(); fresh.Enabled = !fresh.Enabled; _excelImportStore.Save(fresh); ConfigureDataProvider(); _snapshot = null; window.Close(); await LoadAsync("Dashboard"); };
        panel.Children.Add(mode);
        panel.Children.Add(new TextBlock { Text = "Օրվա անկանխիկ վաճառք (ոչ բանկի մնացորդ)։ Սա փոխարինում է տվյալ օրվա նախորդ թիվը։ Նշեք նաև 0, եթե ամբողջ վաճառքը կանխիկ է։", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,15,0,5) });
        var date = new DatePicker { SelectedDate = _selectedDate.ToDateTime(TimeOnly.MinValue) };
        var amount = new TextBox { Text = (_cashDayOverrideStore.Load().Days.LastOrDefault(x=>x.Date==_selectedDate)?.NonCash ?? state.NonCash.LastOrDefault(x => x.Date == _selectedDate)?.Amount)?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "" };
        var save = new Button { Content = "Պահպանել օրվա անկանխիկ վաճառքը" };
        save.Click += async (_,_) =>
        {
            if (date.SelectedDate is null || !PendingOrderEditWindow.TryAmount(amount.Text,out var value))
            { MessageBox.Show("Նշեք ամսաթիվ և ոչ բացասական թիվ։"); return; }
            try
            {
                var day = DateOnly.FromDateTime(date.SelectedDate.Value); var fresh = _excelImportStore.Load();
                var auto=AutomaticCashDays(RawCashDeskMovements()).GetValueOrDefault(day);
                var sales = _cashDayOverrideStore.Load().Days.LastOrDefault(x=>x.Date==day)?.Sales ?? auto?.Sales;
                if(sales is null) throw new InvalidOperationException("Նախ ներմուծեք կամ գրանցեք այդ օրվա վաճառքը։");
                if (value > sales) throw new InvalidOperationException("Անկանխիկ գումարը չի կարող գերազանցել օրվա վաճառքը։");
                _cashDayOverrideStore.SaveNonCash(day,value,"Տնօրեն · Excel ներմուծման էջ");
                _snapshot = null; window.Close(); await LoadAsync("Dashboard");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message,"Գումարը չի պահպանվել"); }
        };
        panel.Children.Add(date); panel.Children.Add(amount); panel.Children.Add(save);
        panel.Children.Add(new TextBlock { Text = "Մատակարարների պարտքի ստուգում՝ Excel տվյալներով", FontSize = 18, Margin = new Thickness(0,15,0,5) });
        panel.Children.Add(new TextBlock { Text = "Սա առանձին համադրման հաշվարկ է․ չի վերագրում ծրագրի պարտքերը։ Սկզբնական պարտքը նշեք «Մատակարարի պարտք» կոճակով՝ ժամանակահատվածին նախորդող օրվա համար։", TextWrapping = TextWrapping.Wrap });
        foreach (var supplierName in state.Batches.SelectMany(x=>x.Receipts).Select(x=>x.Supplier).Distinct().Order())
        {
            var anchor = _supplierDebtHistory.Where(x=>x.Supplier==supplierName && x.EffectiveDate<=_selectedDate &&
                x.Reason.Contains("հիմքային")).OrderByDescending(x=>x.EffectiveDate).ThenByDescending(x=>x.ChangedAt).FirstOrDefault();
            var debtResult = ExcelDebtCalculator.Calculate(state,supplierName,_selectedDate,anchor?.EffectiveDate,anchor?.NewDebt ?? 0m);
            panel.Children.Add(new TextBlock { Text = supplierName + " · " + (debtResult.Debt is { } debt ? $"{debt:N2} ֏" : "Չի հաշվարկվել") + "\n" + debtResult.Warning, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,4) });
        }
        var productRows = state.Batches.SelectMany(x => x.Sales).Where(x => x.Date == _selectedDate).ToList();
        panel.Children.Add(new TextBlock { Text = $"Վաճառքի ապրանքային վերլուծություն · {_selectedDate:dd.MM.yyyy}", FontSize = 18, Margin = new Thickness(0,15,0,5) });
        panel.Children.Add(new TextBlock { Text = $"Վաճառքի փաստաթղթեր՝ {productRows.Select(x => x.Document).Distinct().Count()} · ՀԴՄ համարով չեկեր՝ {productRows.Where(x => x.Receipt != "").Select(x => (x.Shift,x.Receipt)).Distinct().Count()}", TextWrapping = TextWrapping.Wrap });
        foreach (var group in productRows.GroupBy(x => (x.ProductCode,x.Product,x.Unit,x.Supplier)).OrderByDescending(g => g.Sum(x => x.Sales)))
            panel.Children.Add(new TextBlock { Text = $"{group.Key.Product} · {group.Key.Supplier}\n{group.Sum(x => x.Quantity):N3} {group.Key.Unit} · վաճառք {group.Sum(x => x.Sales):N2} ֏ · ինքնարժեք {group.Sum(x => x.Cost):N2} ֏ · շահույթ {group.Sum(x => x.Sales-x.Cost):N2} ֏", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,4) });
        panel.Children.Add(new TextBlock { Text = "Ներմուծված տվյալներ և զգուշացումներ", FontSize = 18, Margin = new Thickness(0,20,0,8) });
        foreach (var b in state.Batches.OrderByDescending(x => x.ImportedAt))
            panel.Children.Add(new TextBlock { Text = $"{b.FileName} · {b.ImportedAt:dd.MM.yyyy HH:mm}\n" + string.Join("\n",b.Warnings), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,12) });
        foreach (var entry in state.NonCash.TakeLast(30).Reverse())
            panel.Children.Add(new TextBlock { Text = $"{entry.Date:dd.MM.yyyy} · անկանխիկ՝ {entry.Amount:N2} ֏ · մուտք՝ {entry.ChangedAt:dd.MM HH:mm}" });
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; window.ShowDialog();
    }

    private bool ConfirmImportPreview(string text)
    {
        var window = new Window { Owner = this, Title = "Ներմուծման նախադիտում", Width = 860, Height = 660, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new DockPanel { Margin = new Thickness(20) };
        var button = new Button { Content = "Հաստատել ներմուծումը և միացնել Excel ռեժիմը", Padding = new Thickness(10) };
        button.Click += (_,_) => window.DialogResult = true;
        DockPanel.SetDock(button,Dock.Bottom); panel.Children.Add(button);
        panel.Children.Add(new ScrollViewer { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } });
        window.Content = panel; return window.ShowDialog() == true;
    }
}
