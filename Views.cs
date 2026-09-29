using System.Windows.Controls;
using System.Windows.Media;

namespace PatarikAIOS;

public static partial class Views
{
    private static TextBlock Text(string value, double size = 14, FontWeight? weight = null, Brush? color = null) => new()
    { Text = value, FontSize = size, FontWeight = weight ?? FontWeights.Normal, Foreground = color ?? BrushFor("#1E293B"), TextWrapping = TextWrapping.Wrap };

    private static Border Card(UIElement child) => new() { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(20), Margin = new Thickness(0, 0, 12, 16), Child = child, BorderBrush = BrushFor("#DCE5F1"), BorderThickness = new Thickness(1) };
    private static string A(decimal amount) => $"{amount:N0} ֏";

    public static UIElement Dashboard(
        DashboardSnapshot s,
        IReadOnlyList<CompletedPayment> completedPayments,
        IReadOnlyList<RequiredPaymentTemplate> requiredPayments,
        IReadOnlyList<SupplierWeekPlanRow> supplierRows,
        IReadOnlyList<EmployeeSupplierAction> employeeSupplierActions,
        CashDailySummary? cashSummary,
        AvailableFundsBreakdown funds,
        int pendingApprovals,
        Action openFunds,
        Action openPayments,
        Action openRisks,
        Action openApprovals,
        IReadOnlyList<(DateOnly Date, decimal? Amount)>? salesTrend = null)
    {
        var root = new StackPanel();
        if (!string.IsNullOrWhiteSpace(s.Sales.DataWarning)) root.Children.Add(WarningBanner(s.Sales.DataWarning));
        // Use the same rule as the Payments page: supplier rows for the selected
        // day + API/manual plans + recurring non-supplier expenses.
        var plannedPayments = supplierRows.Sum(x => x.PaymentAmount + x.OldDebtPayment) +
            s.Payments.Where(p => p.DueDate == s.Date).Sum(p => p.Amount) +
            requiredPayments.Where(p => RequiredPaymentRules.AppliesOn(p, s.Date)).Sum(p => p.Amount);
        root.Children.Add(TileRow(
            ShowcaseMetric("Վաճառք", s.Sales.SalesDisplay, s.Sales.SalesAvailable && s.Sales.ComparisonAvailable ? ChangeHint(s.Sales.SalesChange, "նախորդ օրվա համեմատ") : "Ընտրված օրվա վաճառք", "▥", "#0962FF"),
            ShowcaseMetric("Ինքնարժեք", s.Sales.CostDisplay, "Վաճառված ապրանքների արժեքը", "◇", "#12A875"),
            ShowcaseMetric("Շահույթ", s.Sales.ProfitDisplay, "Վաճառք − ինքնարժեք", "◷", "#E99A16"),
            ShowcaseMetric("Կտրոններ", s.Sales.ReceiptCount.ToString("N0"), "Միջին՝ " + A(s.Sales.AverageReceipt), "▤", "#8B5CF6")));
        var chart = new StackPanel(); chart.Children.Add(Text("Վաճառքի գրաֆիկ (7 օր)", 18, FontWeights.SemiBold));
        chart.Children.Add(new SalesTrendVisual(salesTrend ?? [(s.Date, s.Sales.SalesAvailable ? s.Sales.SalesAmount : null)]) { Height = 245, Margin = new Thickness(0,14,0,0) });
        chart.Children.Add(Text("Բաց թողնված կետերը տվյալների բացակայություն են, ոչ թե զրո վաճառք։", 11, null, BrushFor("#72839C")));
        var fundsPanel = new StackPanel(); fundsPanel.Children.Add(Text("Հասանելի միջոցների կառուցվածք", 18, FontWeights.SemiBold));
        fundsPanel.Children.Add(new FundsRingVisual(funds) { Height = 220, Margin = new Thickness(0,8,0,8) });
        var fundsButton = new Button { Content = "Մնացորդների մանրամասները", HorizontalAlignment = HorizontalAlignment.Left }; fundsButton.Click += (_, _) => openFunds(); fundsPanel.Children.Add(fundsButton);
        root.Children.Add(Split(Card(chart), Card(fundsPanel), 1.45));
        var attention = new StackPanel(); attention.Children.Add(Text("Ուշադրության համար", 18, FontWeights.SemiBold));
        attention.Children.Add(TileRow(
            ShowcaseMetric("Ռիսկեր", s.Recommendations.Count(x => x.Severity == Severity.Critical).ToString(), "Տեսնել խնդիրները", "!", "#E5484D", openRisks),
            ShowcaseMetric("Հաստատումներ", pendingApprovals.ToString(), "Բացել ցանկը", "✓", "#0962FF", openApprovals)));
        var paymentsButton = new Button { Content = "Վճարումների պլան՝ " + A(plannedPayments), HorizontalAlignment = HorizontalAlignment.Left }; paymentsButton.Click += (_, _) => openPayments(); attention.Children.Add(paymentsButton);
        root.Children.Add(Split(PaymentSummaryBlock(s, completedPayments, requiredPayments, supplierRows, employeeSupplierActions), Card(attention), 1.2));
        root.Children.Add(EarlyWarningBlock(s, funds));
        if (cashSummary is not null) root.Children.Add(CashDocumentSummaryBlock(cashSummary));
        root.Children.Add(SalesBlock(s.Sales));
        root.Children.Add(Section("⚠ AI ուշադրության կենտրոն", s.Recommendations.Take(3).Select(r => $"{Icon(r.Severity)}  {r.Title}\n{r.Finding}\nԱռաջարկ՝ {r.SuggestedAction}")));
        root.Children.Add(Section("✅ Այսօրվա առաջադրանքներ", s.Tasks.Select(t => $"{t.Owner} · {t.Task}\nԺամկետ՝ {t.Deadline} · {t.Status}")));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement Salaries(DateOnly selectedDate, IReadOnlyList<SalaryAccrual> accruals, IReadOnlyList<SalaryPayment> payments, IReadOnlyList<PendingSalaryEmployee> pendingEmployees, Action addAccrual, Action addPayment, Action<string> openEmployee, Action<Guid> approveEmployee, Action<Guid> rejectEmployee, Action<string>? removeEmployee = null)
    {
        var weekStart = SalaryRules.WeekStart(selectedDate);
        var employees = accruals.Select(x => x.Employee).Concat(payments.Select(x => x.Employee)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var root = new StackPanel();
        root.Children.Add(Text("Աշխատավարձերի հաշվառում", 19, FontWeights.SemiBold));
        root.Children.Add(Text($"Հաշվարկային շաբաթ՝ {weekStart:dd.MM.yyyy} – {weekStart.AddDays(6):dd.MM.yyyy}. Աշխատավարձը վճարման ենթակա է կիրակի օրը։", 13, null, BrushFor("#64748B")));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 12) };
        var add = new Button { Content = "＋ Օրական աշխատավարձ", Background = BrushFor("#0F766E"), Foreground = Brushes.White };
        add.Click += (_, _) => addAccrual();
        var pay = new Button { Content = "✓ Աշխատավարձ վճարել", Margin = new Thickness(10, 0, 0, 0) };
        pay.Click += (_, _) => addPayment();
        actions.Children.Add(add); actions.Children.Add(pay); root.Children.Add(actions);

        if (pendingEmployees.Count > 0)
        {
            var pending = new StackPanel();
            pending.Children.Add(Text("Նոր աշխատողներ՝ տնօրենի հաստատման սպասումով", 16, FontWeights.SemiBold, BrushFor("#B45309")));
            foreach (var item in pendingEmployees.OrderBy(x => x.RequestedAt))
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
                row.Children.Add(Text($"{item.ProposedAccrual.Employee} · {item.ProposedAccrual.Date:dd.MM} · {A(item.ProposedAccrual.Amount)} · {item.ProposedAccrual.Note}", 13));
                var approve = new Button { Content = "Հաստատել", Margin = new Thickness(10, 0, 0, 0) }; approve.Click += (_, _) => approveEmployee(item.Id);
                var reject = new Button { Content = "Մերժել", Margin = new Thickness(6, 0, 0, 0) }; reject.Click += (_, _) => rejectEmployee(item.Id);
                row.Children.Add(approve); row.Children.Add(reject); pending.Children.Add(row);
            }
            root.Children.Add(Card(pending));
        }

        var weeklyAccrued = SalaryRules.AccruedForWeek(accruals, weekStart);
        var weeklyPaid = SalaryRules.PaidForWeek(payments, weekStart);
        // Weekly figures explain this week's work; total figures keep an
        // earlier unpaid balance visible until it is actually paid.
        var totalAccrued = accruals.Sum(x => x.Amount);
        var totalPaid = payments.Sum(x => x.Amount);
        var totalBalance = totalAccrued - totalPaid;
        root.Children.Add(TileRow(
            ShowcaseMetric("Շաբաթվա աշխատավարձ", A(weeklyAccrued), "Գրանցված գումար", "♙", "#0962FF"),
            ShowcaseMetric("Շաբաթվա վճարված", A(weeklyPaid), "Փաստացի վճարումներ", "✓", "#12A875"),
            ShowcaseMetric("Շաբաթվա մնացորդ", A(weeklyAccrued-weeklyPaid), "Այս շաբաթվա հաշվարկ", "◷", "#E99A16"),
            ShowcaseMetric("Ընդհանուր պարտք", A(totalBalance), "Ներառում է նախկին շաբաթները", "֏", "#8B5CF6")));

        var grid = NewGrid("Աշխատող", "Շաբաթվա գեներացված", "Շաբաթվա վճարված", "Շաբաթվա մնացորդ", "Ընդհանուր մնացորդ", "Վերջին գրառում", "");
        foreach (var employee in employees)
        {
            var personAccruals = accruals.Where(x => string.Equals(x.Employee, employee, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Date).ToList();
            var accrued = SalaryRules.AccruedForWeek(accruals, weekStart, employee);
            var paid = SalaryRules.PaidForWeek(payments, weekStart, employee);
            var totalEmployeeAccrued = accruals.Where(x => string.Equals(x.Employee, employee, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            var totalEmployeePaid = payments.Where(x => string.Equals(x.Employee, employee, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            var latest = personAccruals.FirstOrDefault();
            AddSalaryEmployeeRow(grid, employee, A(accrued), A(paid), A(accrued - paid), A(totalEmployeeAccrued - totalEmployeePaid), latest is null ? "—" : $"{latest.Date:dd.MM} · {latest.Note}", openEmployee, removeEmployee);
        }
        root.Children.Add(Card(new StackPanel { Children = { Text("Աշխատողներ", 16, FontWeights.SemiBold), grid } }));
        root.Children.Add(Section("Ինչպես է աշխատում", new[]
        {
            "Մենեջերը ամեն օր գրանցում է տվյալ աշխատողի փաստացի գեներացված գումարը։",
            "Կիրակի օրը կուտակված, չվճարված գումարը մտնում է պարտադիր շաբաթական ծախսերի և ֆինանսական վերլուծության մեջ։",
            "Վճարելուց հետո գումարը գրանցվում է տվյալ շաբաթի աշխատողի անվան դիմաց, իսկ մնացորդը անմիջապես նվազում է։"
        }));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static void AddSalaryEmployeeRow(Grid grid, string employee, string accrued, string paid, string balance, string totalBalance, string latest, Action<string> openEmployee, Action<string>? removeEmployee = null)
    {
        var row = grid.RowDefinitions.Count; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var open = new Button { Content = "Խմբագրել", Padding = new Thickness(8, 3, 8, 3) };
        open.Click += (_, _) => openEmployee(employee);
        var actions = new WrapPanel(); actions.Children.Add(open);
        if(removeEmployee is not null) { var remove=new Button { Content="Հեռացնել", Foreground=Brushes.Firebrick, Padding=new Thickness(8,3,8,3) }; remove.Click+=(_,_)=>removeEmployee(employee); actions.Children.Add(remove); }
        var cells = new UIElement[] { Text(employee), Text(accrued), Text(paid), Text(balance), Text(totalBalance), Text(latest), actions };
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is FrameworkElement element) element.Margin = new Thickness(0, 8, 5, 8);
            Grid.SetRow(cells[i], row); Grid.SetColumn(cells[i], i); grid.Children.Add(cells[i]);
        }
    }

    public static UIElement CashMovements(DateOnly selectedDate, IReadOnlyList<CashDayLedger> days, IReadOnlyList<CashLedgerMovement> details, string? syncStatus = null, Action<DateOnly>? editDay = null)
    {
        var root = new StackPanel();
        root.Children.Add(Text("Կանխիկի շարժ", 19, FontWeights.SemiBold));
        root.Children.Add(Text($"Ամիս՝ {selectedDate:MM.yyyy}. Վերևի աղյուսակը ցույց է տալիս օրական ամփոփումը, ներքևինը՝ ընտրված օրվա փաստաթղթերը։", 13, null, BrushFor("#64748B")));
        if (!string.IsNullOrWhiteSpace(syncStatus))
            root.Children.Add(Text(syncStatus, 13, FontWeights.SemiBold, BrushFor("#B45309")));
        var closing = days.LastOrDefault(x => x.Date <= selectedDate);
        root.Children.Add(TileRow(
            ShowcaseMetric("0001 · Դրամարկղ", closing is null ? "Տվյալ չկա" : A(closing.CashDeskClosing), "Ընտրված օրվա մնացորդ", "֏", "#0962FF"),
            ShowcaseMetric("0002 · Պահոց", closing is null ? "Տվյալ չկա" : A(closing.VaultClosing), "Ընտրված օրվա մնացորդ", "▣", "#12A875"),
            ShowcaseMetric("Ընդհանուր կանխիկ", closing is null ? "Տվյալ չկա" : A(closing.TotalCash), "Ըստ գործող դրամարկղային հաշվարկի", "◷", "#8B5CF6")));
        root.Children.Add(Text("0001 մնացորդ = նախորդ մնացորդ + ամբողջ վաճառք − անկանխիկ + այլ մուտք − ելք։ Ձեռքով թիվը փոխարինում է ավտոմատին։",12,null,BrushFor("#64748B")));
        root.Children.Add(Text("Օրվա ամփոփ թվի ուղղումը չի փոխում առանձին մատակարարի պարտքը կամ սկզբնական փաստաթուղթը։ Փոխանցման համար օգտագործեք «Շարժ գրանցել»։",12,null,BrushFor("#64748B")));
        var daily = NewGrid("Օր", "Վաճառք / մուտք", "Անկանխիկ", "Այլ մուտք 0001", "0001 ելք", "0001 մն.", "0002 մուտք", "0002 ելք", "0002 մն.", "Ընդ. կանխիկ", "");
        foreach (var day in days)
        {
            var edit = new Button { Content = day.IsManual ? "✎ Ձեռքով" : "Խմբագրել", IsEnabled = editDay is not null, Padding = new Thickness(6,4,6,4) };
            edit.Click += (_,_) => editDay?.Invoke(day.Date);
            AddApprovalRow(daily,Text(day.Date.ToString("dd.MM")),Text(A(day.GrossSales)),Text(A(day.NonCash)),Text(A(day.OtherCashIn)),Text(A(day.CashDeskOut)),Text(A(day.CashDeskClosing)),Text(A(day.VaultIn)),Text(A(day.VaultOut)),Text(A(day.VaultClosing)),Text(A(day.TotalCash)),edit);
        }
        root.Children.Add(Card(new StackPanel { Children = { Text("Օրական ամփոփում", 16, FontWeights.SemiBold), daily } }));

        var detail = NewGrid("Փաստաթուղթ", "Դրամարկղ", "Տեսակ", "Գործընկեր / պատճառ", "Գումար");
        foreach (var item in details)
        {
            var cashDesk = string.IsNullOrWhiteSpace(item.SourceCashDesk) ? item.TargetCashDesk ?? "—" : item.SourceCashDesk;
            var direction = string.IsNullOrWhiteSpace(item.SourceCashDesk) ? "Մուտք" : item.IsInternalTransfer ? $"Փոխանցում → {item.TargetCashDesk}" : "Ելք";
            AddRow(detail, item.DocumentNumber, cashDesk, direction, string.IsNullOrWhiteSpace(item.Partner) ? item.ContractOrReason : item.Partner, A(item.Amount));
        }
        root.Children.Add(Card(new StackPanel { Children = { Text($"Փաստաթղթեր՝ {selectedDate:dd.MM.yyyy}", 16, FontWeights.SemiBold), detail } }));
        if (details.Count == 0) root.Children.Add(Text("Այս օրվա համար ներմուծված դրամարկղային փաստաթուղթ չկա։", 13, null, BrushFor("#64748B")));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement Finance(DashboardSnapshot s, WeeklyFinancialPlan weeklyPlan, CashFlowPolicy policy, Action openPolicy)
    {
        var root = new StackPanel(); root.Children.Add(Text("Ֆինանսական կենտրոն", 19, FontWeights.SemiBold));
        root.Children.Add(Text("Շաբաթվա պլանը վաճառքի կանխատեսումը, կոշտ ծախսերը, աշխատավարձը և մատակարարային վճարները միացնում է մեկ բյուջեի մեջ։", 13, null, BrushFor("#64748B")));
        var policyButton = new Button { Content = "⚙ Ֆինանսական կանոններ", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 10) };
        policyButton.Click += (_, _) => openPolicy();
        root.Children.Add(policyButton);
        root.Children.Add(TileRow(
            ShowcaseMetric("Շաբաթվա բազա", A(weeklyPlan.BaselineWeekSales), "Վաճառքի հաշվարկային հիմք", "▥", "#0962FF"),
            ShowcaseMetric("Պարտադիր վճարներ", A(weeklyPlan.FixedPaymentsDue), "Շաբաթվա պարտավորություններ", "▤", "#E99A16"),
            ShowcaseMetric("Աշխատավարձ", A(weeklyPlan.SalaryPaymentsDue), "Վճարման ենթակա", "♙", "#8B5CF6"),
            ShowcaseMetric("Ազատ գումար", SignedPlain(weeklyPlan.FreeMoney), weeklyPlan.IsWithinPlan ? "Հասանելի բյուջե" : "Բյուջեի գերազանցում", "֏", weeklyPlan.IsWithinPlan ? "#12A875" : "#E5484D")));
        root.Children.Add(Section("💰 Այս պահի դիրք", new[] { $"Դրամարկղ՝ {A(s.Cash.Cash)}", $"Բանկ՝ {A(s.Cash.Bank)}", $"Ընդհանուր հասանելի՝ {A(s.Cash.Available)}", $"Պաշտպանական պահուստ՝ {A(policy.MinimumReserve)}" }));

        var budget = NewGrid("Ցուցանիշ", "Գումար", "Բացատրություն");
        AddRow(budget, "Շաբաթվա վաճառքի բազա", A(weeklyPlan.BaselineWeekSales), "Նախորդ 7 օրվա միջին կամ ձեր նշած հիմք");
        AddRow(budget, "Վաճառքի շեղում", SignedPlain(weeklyPlan.SalesVariance), weeklyPlan.SalesVariance >= 0m ? "Փաստացի/ընթացիկ վաճառքը պլանից բարձր է" : "Փաստացի/ընթացիկ վաճառքը պլանից ցածր է");
        AddRow(budget, "Կոշտ վճարումներ", A(weeklyPlan.FixedPaymentsDue), "Պարտադիր վճարներ, որոնք ընկնում են այս 7 օրվա մեջ");
        AddRow(budget, "Աշխատավարձ", A(weeklyPlan.SalaryPaymentsDue), "Շաբաթվա վճարման ենթակա աշխատավարձ");
        AddRow(budget, "Մատակարարների վճարներ", A(weeklyPlan.SupplierPaymentsDue), "Նոր պատվերի և հին պարտքի վճարումներ");
        AddRow(budget, "Պաշտպանական պահուստ", A(weeklyPlan.MinimumReserve), "Չի առաջարկվում ծախսել առանց ձեր որոշման");
        AddRow(budget, "Շաբաթվա ազատ գումար", SignedPlain(weeklyPlan.FreeMoney), weeklyPlan.IsWithinPlan ? "Կարելի է օգտագործել լրացուցիչ ճկուն վճարների կամ պահուստի համար" : "Պլանը գերազանցում է շաբաթվա վաճառքային բյուջեն");
        root.Children.Add(Card(new StackPanel { Children = { Text("Շաբաթվա վճարային բյուջե", 16, FontWeights.SemiBold), budget } }));

        var dailyBudget = NewGrid("Օր", "Վաճառք", "Կոշտ ծախսի պահուստ", "Աշխատավարձի կուտակում", "Պլան. վճարում", "Օրվա սահման", "Շեղում", "Հաջորդ օրերի նոր սահման");
        foreach (var day in weeklyPlan.Days)
            AddRow(dailyBudget, day.Date.ToString("dd.MM"), A(day.SalesPlan), A(day.FixedCostReserve), A(day.SalaryAccrual), A(day.PlannedPayments), A(day.DailyPaymentLimit), SignedPlain(day.DifferenceFromLimit), A(day.NextDaysDailyLimit));
        root.Children.Add(Card(new StackPanel { Children =
        {
            Text("Օրական սահմանաչափի վերահաշվարկ", 16, FontWeights.SemiBold),
            Text("Եթե օրվա վճարումը սահմանաչափից բարձր է, տարբերությունը հանվում է հաջորդ օրերի թույլատրելի գումարից։ Եթե պակաս է՝ հաջորդ օրերի սահմանը մեծանում է։", 12, null, BrushFor("#64748B")),
            dailyBudget
        }}));

        var grid = NewGrid("Ամսաթիվ", "Սպասվող մուտք", "Սպասվող ելք", "Օրվա վերջի կանխատեսում");
        foreach (var f in s.Forecast) AddRow(grid, f.Date.ToString("dd.MM"), A(f.ExpectedIncome), A(f.ExpectedOutflow), A(f.ClosingBalance));
        root.Children.Add(Card(new StackPanel { Children = { Text("📈 7-օրյա cash-flow կանխատեսում", 16, FontWeights.SemiBold), grid } }));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement PurchasePlan(DateOnly planningDate, DateOnly deliveryDate, IReadOnlyList<SupplierWeekPlanRow> scheduledSuppliers, IReadOnlyList<PurchaseProposal> proposals)
    {
        var root = new StackPanel();
        root.Children.Add(Text("Վաղվա պատվերներ", 19, FontWeights.SemiBold));
        root.Children.Add(Text($"Այսօր՝ {planningDate:dd.MM.yyyy} · Մատակարարում՝ {deliveryDate:dd.MM.yyyy} ({ArmenianWeekdayLabel(deliveryDate.DayOfWeek)})", 13, null, BrushFor("#64748B")));
        var scheduledNames = scheduledSuppliers.Select(x => x.Supplier).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        root.Children.Add(Section("Վաղվա մատակարարներ", scheduledNames.Count == 0 ? new[] { "Վաղվա համար մատակարար չի նախատեսված։" } : scheduledNames));

        if (proposals.Count == 0)
        {
            root.Children.Add(Section("Պատվերի առաջարկ", new[]
            {
                "ՀԾ-ում առաջարկվող ապրանք չկա։ Ստուգեք ապրանքների նվազագույն/առավելագույն մնացորդների և հիմնական մատակարարի դաշտերը։",
                "Ծրագիրը առաջարկ է ստեղծում, երբ մնացորդը հասել է նվազագույն սահմանին կամ ՀԾ-ն արդեն ունի պատվերի քանակ։"
            }));
            return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        var total = proposals.Sum(x => x.Amount);
        root.Children.Add(Section("Առաջարկվող ընդհանուր պատվեր", new[] { $"{A(total)} · {proposals.Count} ապրանք · {proposals.Select(x => x.Supplier).Distinct().Count()} մատակարար" }));
        foreach (var supplier in proposals.GroupBy(x => x.Supplier).OrderBy(x => x.Key))
        {
            var grid = NewGrid("Ապրանք", "Պահեստ", "Մնացորդ", "Նվազագույն", "Առաջարկ", "Գին", "Գումար", "Պատճառ");
            foreach (var item in supplier)
                AddRow(grid, item.Product, item.Storage, $"{item.CurrentQuantity:0.##} {item.Unit}", $"{item.MinimumQuantity:0.##} {item.Unit}",
                    $"{item.ProposedQuantity:0.##} {item.Unit}", A(item.UnitCost), A(item.Amount), item.Reason);
            var body = new StackPanel();
            body.Children.Add(Text($"{supplier.Key} · {A(supplier.Sum(x => x.Amount))}", 16, FontWeights.SemiBold, BrushFor("#0F766E")));
            body.Children.Add(grid);
            root.Children.Add(Card(body));
        }
        root.Children.Add(Text("Առաջարկը չի ուղարկվում մատակարարին ինքնուրույն․ այն նախատեսված է ձեր հաստատման և քանակների փոփոխության համար։", 13, null, BrushFor("#64748B")));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement Suppliers(DashboardSnapshot s, IReadOnlyList<SupplierDeliveryPattern> deliveryPatterns)
    {
        var root = new StackPanel();
        root.Children.Add(Text("Մատակարարներ · պատվերներ · պարտքերի շարժ", 19, FontWeights.SemiBold));
        root.Children.Add(Text("Յուրաքանչյուր տող ցույց է տալիս՝ տվյալ օրը պատվերը որքան է, ինչ մասն է վճարվել նույն պատվերից և որքան հին պարտք է մարվել։", 13, null, BrushFor("#64748B")));

        if (deliveryPatterns.Count > 0)
        {
            root.Children.Add(Text("Մատակարարների մեկնարկային ցուցակ — WarehouseDocument.xlsx", 16, FontWeights.SemiBold, BrushFor("#0F766E")));
            var directory = NewGrid("Մատակարար", "Ստացման օրեր", "Ստացումների քանակ", "Միջին ստացում", "Առաջարկվող գումար");
            foreach (var group in deliveryPatterns.Where(x => !x.IsOneTime).GroupBy(x => x.Supplier).OrderBy(x => x.Key))
            {
                var patterns = group.ToList();
                var days = string.Join(", ", patterns.OrderBy(x => DayOrder(x.Weekday)).Select(x => ArmenianWeekdayLabel(x.Weekday)));
                var count = patterns.Sum(x => x.DeliveryCount);
                var average = patterns.Where(x => x.DeliveryCount > 0).Select(x => x.AverageOrderAmount).DefaultIfEmpty(0m).Average();
                var suggested = patterns.Sum(x => x.SuggestedOrderAmount);
                AddRow(directory, group.Key, days, count.ToString(), A(average), A(suggested));
            }
            root.Children.Add(Card(directory));
        }

        foreach (var day in s.SupplierMovements.GroupBy(x => x.Date).OrderBy(x => x.Key))
        {
            var movements = day.ToList();
            var opening = movements.Sum(x => x.OpeningDebt);
            var orders = movements.Sum(x => x.OrderAmount);
            var orderPayments = movements.Sum(x => x.PaymentForOrder);
            var oldDebtPayments = movements.Sum(x => x.OldDebtPayment);
            var change = movements.Sum(x => x.DebtChange);
            var closing = opening + change;
            var label = $"{ArmenianWeekdayLabel(day.Key.DayOfWeek)} · {day.Key:dd.MM.yyyy}";
            root.Children.Add(Text(label, 16, FontWeights.SemiBold, BrushFor("#0F766E")));

            // Keep the daily accounting table intentionally compact for the owner's workflow.
            var grid = NewGrid("Մատակարար", "Պատվեր", "Վճարում", "Հին թվի վճարում", "Պարտք");
            foreach (var x in movements)
                AddRow(grid, x.IsPlanned ? $"{x.Supplier}\n(պլանավորված)" : x.Supplier, A(x.OrderAmount), PaymentText(x), OldPaymentText(x), A(x.ClosingDebt));

            var total = new StackPanel();
            total.Children.Add(Text($"Օրվա ընդհանուր հաշվարկ", 14, FontWeights.SemiBold));
            total.Children.Add(Text($"Սկզբնական պարտք՝ {A(opening)}   |   Նոր պատվերներ՝ {A(orders)}   |   Պատվերից վճարված՝ {A(orderPayments)}   |   Հին պարտքի վճարում՝ {A(oldDebtPayments)}"));
            total.Children.Add(Text($"Ընդհանուր պարտքի փոփոխություն՝ {Signed(change)}   →   Օրվա վերջում՝ {A(closing)}", 14, FontWeights.SemiBold, change > 0 ? BrushFor("#B91C1C") : change < 0 ? BrushFor("#0F766E") : BrushFor("#475569")));
            root.Children.Add(Card(new StackPanel { Children = { grid, new Separator { Margin = new Thickness(0, 12, 0, 10) }, total } }));
        }
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement Suppliers(IReadOnlyList<SupplierWeekPlanRow> rows, IReadOnlyList<Supplier> supplierDebts, IReadOnlyList<PartnerDebt> importedDebts, IReadOnlyList<EmployeeSupplierAction> employeeActions, IReadOnlyList<SupplierStatusChange> statusChanges, IReadOnlyList<SupplierNote> supplierNotes, WeeklyFinancialPlan weeklyPlan, Action<SupplierWeekPlanRow, decimal, decimal, decimal, decimal> saveRow, Action<string> showSupplierStatus, Action<string> showSupplierDebtHistory, Action<SupplierWeekPlanRow, EmployeeSupplierAction?> editStatus, Action<SupplierWeekPlanRow> addNote, Action<SupplierWeekPlanRow> showAnalysis, Func<IReadOnlyList<SupplierRowEdit>, Task> saveAll, IDictionary<string, string[]> drafts, string? saveStatus = null)
    {
        var root = new StackPanel();
        var footer = new StackPanel();
        var top = TileRow(ShowcaseMetric("Պատվերներ", A(rows.Sum(x => x.OrderAmount)), "Պահպանված պատվերների գումար", "▤", "#0962FF"), ShowcaseMetric("Վճարում", A(rows.Sum(x => x.PaymentAmount)), "Պատվերի դիմաց", "▣", "#12A875"), ShowcaseMetric("Հին պարտքի վճարում", A(rows.Sum(x => x.OldDebtPayment)), "Նախորդ պարտավորություններ", "◷", "#E99A16"));
        root.Children.Add(Text("Օրվա պատվերների գրանցում", 16, FontWeights.SemiBold));
        if (!string.IsNullOrWhiteSpace(saveStatus)) root.Children.Add(Text(saveStatus, 13, null, BrushFor("#166534")));
        root.Children.Add(Text("Գումարները փոփոխելուց հետո պահպանեք առանձին տողը կամ օրվա բոլոր մուտքերը։", 13, null, BrushFor("#64748B")));
        foreach (var day in rows.GroupBy(x => x.Date).OrderBy(x => x.Key))
        {
            var editors = new List<Func<SupplierRowEdit?>>();
            root.Children.Add(Text($"{ArmenianWeekdayLabel(day.Key.DayOfWeek)} · {day.Key:dd.MM.yyyy}", 16, FontWeights.SemiBold, BrushFor("#0F766E")));
            var grid = NewGrid("Մատակարար", "Պատվեր", "Վճարում", "Հին պարտքի վճարում", "Պարտք / փոփոխություն", "Կարգավիճակ", "Նշում", "Գործողություն");
            var widths = new double[] { 190, 100, 100, 100, 132, 130, 160, 196 };
            for (var i = 0; i < widths.Length; i++) { grid.ColumnDefinitions[i].Width = new GridLength(widths[i], GridUnitType.Star); grid.ColumnDefinitions[i].MinWidth = widths[i] * 0.85; }
            foreach (var row in day)
            {
                var currentAction = employeeActions
                    .Where(x => x.Date == row.Date && SupplierNamesMatch(x.Supplier, row.Supplier))
                    .OrderByDescending(x => x.ReportedAt)
                    .FirstOrDefault();
                var manualChange = statusChanges
                    .Where(x => x.Date == row.Date && SupplierNamesMatch(x.Supplier, row.Supplier))
                    .OrderByDescending(x => x.ChangedAt)
                    .FirstOrDefault();
                if (manualChange is not null && (currentAction is null || manualChange.ChangedAt >= currentAction.ReportedAt))
                {
                    var explanation = $"Ձեռքով փոփոխվել է {manualChange.ChangedAt:dd.MM.yyyy HH:mm}-ին։ Նախորդ կարգավիճակ՝ {manualChange.PreviousStatus}.";
                    if (!string.IsNullOrWhiteSpace(manualChange.Note)) explanation += $" Նշում՝ {manualChange.Note}";
                    currentAction = new EmployeeSupplierAction(row.Date, row.Supplier, manualChange.NewStatus, explanation, "owner", manualChange.ChangedBy, manualChange.ChangedAt);
                }
                var latestNote = supplierNotes.Where(note => note.Date == row.Date && SupplierNamesMatch(note.Supplier, row.Supplier)).OrderByDescending(note => note.CreatedAt).FirstOrDefault();
                AddEditableSupplierWeekRow(grid, ApplyKnownDebt(row, supplierDebts, importedDebts), currentAction, latestNote, saveRow, showSupplierStatus, showSupplierDebtHistory, editStatus, addNote, showAnalysis, editors, drafts);
            }
            var orderCount = day.Count(x => x.OrderAmount > 0);
            var orderSum = day.Sum(x => x.OrderAmount);
            var paymentSum = day.Sum(x => x.PaymentAmount);
            var oldDebtSum = day.Sum(x => x.OldDebtPayment);
            var debtChange = orderSum - paymentSum - oldDebtSum;
            var note = debtChange switch { > 0 => "Պարտքն ավելացել է", < 0 => "Պարտքը նվազել է", _ => "Պարտքի փոփոխություն չկա" };
            var summary = new StackPanel();
            summary.Children.Add(Text("Օրվա ամփոփում", 14, FontWeights.SemiBold));
            summary.Children.Add(Text($"Պատվերների քանակ՝ {orderCount} · Պատվերների գումար՝ {A(orderSum)} · Վճարում՝ {A(paymentSum)} · Հին թվի վճարում՝ {A(oldDebtSum)}"));
            summary.Children.Add(Text($"Պարտքի փոփոխություն՝ {Signed(debtChange)} · {note}", 14, FontWeights.SemiBold, debtChange > 0 ? BrushFor("#B91C1C") : debtChange < 0 ? BrushFor("#0F766E") : BrushFor("#475569")));
            var saveAllButton = new Button { Content = "✓ Պահպանել օրվա բոլոր մուտքերը", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 8, 0, 0) };
            saveAllButton.Click += async (_, _) =>
            {
                var edits = editors.Select(read => read()).ToList();
                if (edits.Any(edit => edit is null))
                {
                    MessageBox.Show("Կա սխալ գումար։ Ուղղեք նշված տողը․ ոչինչ չի պահպանվել։", "Սխալ տվյալ");
                    return;
                }
                saveAllButton.IsEnabled = false;
                try { await saveAll(edits.Cast<SupplierRowEdit>().ToList()); }
                finally { saveAllButton.IsEnabled = true; }
            };
            saveAllButton.Background = PresentationTheme.Blue; saveAllButton.Foreground = Brushes.White;
            var footerRow = new DockPanel { Margin = new Thickness(0,0,12,0) };
            saveAllButton.HorizontalAlignment = HorizontalAlignment.Right; saveAllButton.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(saveAllButton,Dock.Right); footerRow.Children.Add(saveAllButton); footerRow.Children.Add(summary);
            root.Children.Add(Card(grid));
            footer.Children.Add(footerRow);
        }
        var bottom = new StackPanel(); bottom.Children.Add(WeeklyPaymentStatus(weeklyPlan)); bottom.Children.Add(footer);
        return AdaptiveOrders(top, root, bottom);
    }

    private static UIElement WeeklyPaymentStatus(WeeklyFinancialPlan plan)
    {
        var today = plan.Days.FirstOrDefault();
        var remainingDays = Math.Max(0, plan.Days.Count - 1);
        var statusColor = plan.IsWithinPlan ? BrushFor("#166534") : BrushFor("#B91C1C");
        var impactText = today is null ? "—" : SignedPlain(today.DifferenceFromLimit);
        var impactHint = today is null ? "" : today.DifferenceFromLimit > 0m
            ? "օգտագործվել է հաջորդ օրերի սահմանաչափից"
            : today.DifferenceFromLimit < 0m ? "մնացել է հաջորդ օրերի համար" : "օրվա սահմանաչափին համընկնում է";
        var panel = new StackPanel();
        panel.Children.Add(Text($"Շաբաթվա ազատ գումար՝ {SignedPlain(plan.FreeMoney)}", 19, FontWeights.Bold, plan.IsWithinPlan ? PresentationTheme.Blue : statusColor));
        panel.Children.Add(Text($"Մնացած {remainingDays} օրվա նոր միջին սահման՝ {A(today?.NextDaysDailyLimit ?? 0m)} / օր", 12, FontWeights.SemiBold));
        var detail = new StackPanel();
        detail.Children.Add(Text($"Այս օրվա սահմանաչափից շեղում՝ {impactText} · {impactHint}", 13, null, BrushFor("#475569")));
        detail.Children.Add(Text(plan.IsWithinPlan
            ? "🟢 Պլանը հավասարակշռված է։ Հաջորդ օրերի սահմանաչափը հաշվարկվել է վերջին փոփոխությունների հիման վրա։"
            : $"🔴 Պլանում պակաս կա՝ {A(Math.Abs(plan.FreeMoney))}։ Վերանայեք միայն ճկուն վճարումները կամ հաստատեք բացառություն։", 13, FontWeights.SemiBold, statusColor));
        panel.Children.Add(new Expander { Header = plan.IsWithinPlan ? "Շաբաթվա փոփոխության մանրամասները" : "⚠ Սահմանաչափը գերազանցված է · մանրամասներ", Content = detail, Margin = new Thickness(0,6,0,0), Foreground = statusColor });
        return Card(panel);
    }

    public static UIElement Payments(DashboardSnapshot s, IReadOnlyList<CompletedPayment> completedPayments, IReadOnlyList<RequiredPaymentTemplate> requiredPayments, IReadOnlyList<SupplierWeekPlanRow> supplierRows, IReadOnlyList<EmployeeSupplierAction> employeeSupplierActions, IReadOnlyList<SalaryPayment> salaryPayments, decimal plannedSalary, Action<RequiredPaymentTemplate> editPayment, Action<RequiredPaymentTemplate> deletePayment)
    {
        var isPast = s.Date < DateOnly.FromDateTime(DateTime.Today);
        var root = new StackPanel(); root.Children.Add(Text("Վճարումների պլան", 19, FontWeights.SemiBold));
        root.Children.Add(Text($"Ընտրված ամսաթիվ՝ {s.Date:dd.MM.yyyy}. Ստորև ցուցադրված են միայն այդ օրվա պլանավորված և փաստացի վճարումները։", 13, null, BrushFor("#64748B")));
        var supplierNames = supplierRows.Select(x => x.Supplier).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var completedForDate = completedPayments.Where(x => x.PaidDate == s.Date).ToList();
        var supplierCompleted = SupplierActualPaymentRows(s.Date, supplierRows, completedForDate, employeeSupplierActions);
        var otherCompleted = completedForDate.Where(x => !supplierRows.Any(row => SupplierNamesMatch(row.Supplier, x.Recipient)) && !x.Recipient.StartsWith("Աշխատավարձ", StringComparison.OrdinalIgnoreCase)).ToList();
        var supplierItems = supplierRows.Where(x => x.PaymentAmount > 0 || x.OldDebtPayment > 0).ToList();
        var supplierPlanItems = s.Payments.Where(x => x.DueDate == s.Date && supplierNames.Contains(x.Supplier)).ToList();
        var supplierPlannedTotal = supplierItems.Sum(x => x.PaymentAmount + x.OldDebtPayment) + supplierPlanItems.Sum(x => x.Amount);
        var supplierActualTotal = supplierCompleted.Sum(x => x.Amount);
        var otherRequired = requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, s.Date)).ToList();
        var otherPlanItems = s.Payments.Where(x => x.DueDate == s.Date && !supplierNames.Contains(x.Supplier)).ToList();
        var otherPlannedTotal = otherRequired.Sum(x => x.Amount) + otherPlanItems.Sum(x => x.Amount);
        var otherActualTotal = otherCompleted.Sum(x => x.Amount);
        var actualSalary = salaryPayments.Where(x => x.PaidDate == s.Date).Sum(x => x.Amount);
        var daySummary = new StackPanel();
        daySummary.Children.Add(PaymentGroupExpander("Աշխատավարձ", plannedSalary, actualSalary, SalaryPaymentDetails(salaryPayments.Where(x => x.PaidDate == s.Date).ToList(), plannedSalary)));
        daySummary.Children.Add(PaymentGroupExpander("Մատակարարների վճարներ", supplierPlannedTotal, supplierActualTotal, SupplierPaymentDetails(supplierItems, supplierPlanItems, supplierCompleted)));
        daySummary.Children.Add(PaymentGroupExpander("Այլ ծախսեր", otherPlannedTotal, otherActualTotal, OtherExpenseDetails(otherRequired, otherPlanItems, otherCompleted)));
        root.Children.Add(Text("Պլանավորված և փաստացի կատարված վճարումների համեմատություն։", 13, null, BrushFor("#64748B")));
        var grid = NewGrid("Ամսաթիվ", "Ստացող", "Գումար", "Կարգ", "Պատճառ");
        // The old full list is intentionally retained in memory for the monthly plan below.
        // The owner sees the selected day's two payment groups instead.
        root.Children.Add(Card(daySummary));

        var requiredGrid = NewGrid("Կատեգորիա", "Անվանում / ստացող", "Գումար", "Վճարման օր", "Կրկնում", "Նշում", "");
        foreach (var item in requiredPayments.Where(x => !isPast && x.IsActive && RequiredPaymentRules.AppliesOn(x, s.Date)).OrderBy(x => x.PaymentDay))
            AddRequiredPaymentRow(requiredGrid, item, editPayment, deletePayment);
        if (requiredPayments.All(x => !x.IsActive || !RequiredPaymentRules.AppliesOn(x, s.Date)))
            AddRow(requiredGrid, "—", "Այս օրվա համար պարտադիր վճարում չկա", "", "", "", "", "");
        root.Children.Add(CollapsibleSection("Պարտադիր վճարների բազա", Card(requiredGrid)));

        var monthlyGrid = NewGrid("Կատեգորիա", "Անվանում / ստացող", "Գումար", "Վճարման օր", "Կրկնում", "Նշում", "");
        var monthItems = requiredPayments.Where(x => RequiredPaymentRules.AppliesInMonth(x, s.Date)).OrderBy(x => x.PaymentDay).ThenBy(x => x.Name).ToList();
        foreach (var item in monthItems) AddRequiredPaymentRow(monthlyGrid, item, editPayment, deletePayment);
        if (!monthItems.Any()) AddRow(monthlyGrid, "—", "Բազայում վճարում չկա", "", "", "", "", "");
        var monthContent = new StackPanel();
        monthContent.Children.Add(Text("Այստեղից կարող եք խմբագրել կամ հեռացնել ցանկացած վճարում՝ առանց այլ օր ընտրելու։", 12, null, BrushFor("#64748B")));
        monthContent.Children.Add(Card(monthlyGrid));
        root.Children.Add(CollapsibleSection("Ընտրված ամսվա պարտադիր վճարումների բազա", monthContent));

        var scheduleGrid = NewGrid("Վճարման օր", "Վճարումներ", "Ընդհանուր գումար");
        foreach (var group in requiredPayments.Where(x => !isPast && RequiredPaymentRules.AppliesOn(x, s.Date)).GroupBy(x => x.PaymentDay).OrderBy(x => x.Key))
            AddRow(scheduleGrid, group.Key.ToString(), string.Join(", ", group.Select(x => x.Name)), A(group.Sum(x => x.Amount)));
        root.Children.Add(CollapsibleSection("Ամսական վճարումների գրաֆիկ", Card(scheduleGrid)));
        return new ScrollViewer { Content = root };
    }

    public static UIElement SupplierSalesAnalysis(DateOnly startDate, DateOnly endDate, IReadOnlyList<SupplierSalesAnalysis> rows)
    {
        var root = new StackPanel();
        root.Children.Add(Text("Վաճառքի վերլուծություն՝ ըստ մատակարարի", 19, FontWeights.SemiBold));
        root.Children.Add(Text($"Ժամանակահատված՝ {startDate:dd.MM.yyyy}–{endDate:dd.MM.yyyy}. Ամսաթիվը փոխելով՝ կփոխվի նաև հաշվարկը։", 13, null, BrushFor("#64748B")));

        if (rows.Count == 0)
        {
            root.Children.Add(Card(Text("Այս ժամանակահատվածում մատակարարի հետ կապված վաճառքի տողեր չգտնվեցին։ Վերլուծությունը ցուցադրում է միայն այն ապրանքները, որոնց խմբաքանակի մատակարարը նշված է ՀԾ-ում։", 14)));
            return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        var totalSales = rows.Sum(x => x.SalesAmount);
        var totalCost = rows.Sum(x => x.CostAmount);
        var metrics = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
        metrics.Children.Add(Metric("Կապված վաճառք", A(totalSales), $"{rows.Count} մատակարար"));
        metrics.Children.Add(Metric("Ինքնարժեք", rows.All(x => x.CostAvailable) ? A(totalCost) : "Տվյալ չկա", "Ըստ վաճառված ապրանքների"));
        metrics.Children.Add(Metric("Շահույթ", rows.All(x => x.CostAvailable) ? A(totalSales - totalCost) : "Չի հաշվարկվել", "Վաճառք − ինքնարժեք"));
        root.Children.Add(metrics);

        var grid = NewGrid("Մատակարար", "Վաճառք", "Ինքնարժեք", "Շահույթ", "Քանակ", "Ապրանք", "Պահեստ");
        foreach (var row in rows)
            AddRow(grid, row.Supplier, A(row.SalesAmount), row.CostDisplay, row.ProfitDisplay, row.Quantity.ToString("N2"), row.ProductCount.ToString(), row.StorageCount.ToString());
        root.Children.Add(Card(new StackPanel { Children = { Text("Մանրամասն", 16, FontWeights.SemiBold), grid } }));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement SupplierAnalysisCard(SupplierAnalysisData data)
    {
        var root = new StackPanel();
        root.Children.Add(Text(data.Supplier, 20, FontWeights.SemiBold, BrushFor("#0F766E")));
        root.Children.Add(Text($"{data.StartDate:dd.MM.yyyy}–{data.EndDate:dd.MM.yyyy} · " + (data.FromHts ? "Տվյալները՝ ՀԾ-ից" : "Տվյալները՝ ծրագրի հաստատված պլանից"), 13, null, BrushFor("#64748B")));

        var receipts = data.Activity.Sum(x => x.ReceiptAmount);
        var payments = data.Activity.Sum(x => x.PaymentForOrder);
        var oldPayments = data.Activity.Sum(x => x.OldDebtPayment);
        var metrics = new WrapPanel { Margin = new Thickness(0, 14, 0, 8) };
        metrics.Children.Add(Metric("Ստացում", A(receipts), "Գնումներ՝ ձեռքբերման գնով"));
        metrics.Children.Add(Metric("Վճարում", A(payments), "Տվյալ ստացման դիմաց"));
        metrics.Children.Add(Metric("Հին պարտքի վճարում", A(oldPayments), "Նախորդ պարտավորություն"));
        metrics.Children.Add(Metric("Պարտքի փոփոխություն", Signed(receipts - payments - oldPayments), "Ընտրված ժամանակահատվածում"));
        metrics.Children.Add(Metric("Ընթացիկ պարտք", A(data.CurrentDebt), "Ընտրված օրվա դրությամբ"));
        root.Children.Add(metrics);

        var history = NewGrid("Ամսաթիվ", "Ստացում", "Վճարում", "Հին պարտք", "Փաստաթուղթ", "Նկարագրություն");
        foreach (var line in data.Activity)
            AddRow(history, line.Date.ToString("dd.MM.yyyy"), A(line.ReceiptAmount), A(line.PaymentForOrder), A(line.OldDebtPayment), line.DocumentNumbers, line.Description);
        if (data.Activity.Count == 0) AddRow(history, "—", "0 ֏", "0 ֏", "0 ֏", "", "Ընտրված ժամանակահատվածում շարժ չի գտնվել");
        root.Children.Add(Card(new StackPanel { Children = { Text("Ստացումներ և վճարումներ", 16, FontWeights.SemiBold), history } }));

        var sales = data.Sales;
        if (!string.IsNullOrWhiteSpace(data.Warning)) root.Children.Add(Text(data.Warning, 13, null, BrushFor("#B45309")));
        var salesMetrics = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
        salesMetrics.Children.Add(Metric("Վաճառք", sales is null ? "Տվյալ չկա" : A(sales.SalesAmount), "Վաճառքի գնով"));
        salesMetrics.Children.Add(Metric("Ինքնարժեք", sales?.CostDisplay ?? "Տվյալ չկա", "Մատակարարի/ձեռքբերման գնով"));
        salesMetrics.Children.Add(Metric("Շահույթ", sales?.ProfitDisplay ?? "Չի հաշվարկվել", "Վաճառք − ինքնարժեք"));
        salesMetrics.Children.Add(Metric("Վաճառված քանակ", (sales?.Quantity ?? 0m).ToString("N2"), $"Ապրանք՝ {sales?.ProductCount ?? 0}"));
        salesMetrics.Children.Add(Metric("Պահեստներ", (sales?.StorageCount ?? 0).ToString(), "Վաճառքի աղբյուրներ"));
        root.Children.Add(Card(new StackPanel { Children = { Text("Վաճառքի վերլուծություն", 16, FontWeights.SemiBold), salesMetrics } }));

        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement DeliverySchedule(IReadOnlyList<SupplierDeliveryPattern> patterns, Action<SupplierDeliveryPattern, decimal> saveAmount)
    {
        var root = new StackPanel(); root.Children.Add(Text("Մատակարարներից ապրանք ստանալու օրեր", 19, FontWeights.SemiBold));
        root.Children.Add(Text("Այս օրերը և գումարները ստեղծված են WarehouseDocument.xlsx-ի ստացումների պատմությունից։ «Առաջարկվող գումար»-ը կարող եք փոխել անմիջապես աղյուսակի տողում կամ վերևի կոճակով։", 13, null, BrushFor("#64748B")));
        if (patterns.Count == 0)
        {
            root.Children.Add(Card(Text("Տվյալներ դեռ չկան։ Սեղմեք «Ներմուծել ստացումներ» և ընտրեք WarehouseDocument.xlsx ֆայլը։", 15))); return new ScrollViewer { Content = root };
        }
        foreach (var group in patterns.GroupBy(x => x.Weekday).OrderBy(x => DayOrder(x.Key)))
        {
            root.Children.Add(Text(ArmenianWeekdayLabel(group.Key), 16, FontWeights.SemiBold, BrushFor("#0F766E")));
            var grid = NewGrid("Մատակարար", "Ստացումների քանակ", "Միջին ստացում", "Առաջարկվող գումար", "Տնօրենի հրահանգ");
            foreach (var x in group.OrderBy(x => x.Supplier)) AddEditableDeliveryRow(grid, x, saveAmount);
            root.Children.Add(Card(grid));
        }
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement Summary(BusinessSummary summary, string filterDescription, Action showMonth, Action showWeek, Action showDay)
    {
        var root = new StackPanel();
        root.Children.Add(Text("Ամփոփում", 19, FontWeights.SemiBold));
        if (!string.IsNullOrWhiteSpace(summary.Sales.DataWarning)) root.Children.Add(WarningBanner(summary.Sales.DataWarning));
        root.Children.Add(Text($"Ժամանակահատված՝ {filterDescription}", 13, null, BrushFor("#64748B")));

        var filters = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 16) };
        var month = new Button { Content = "Այս ամիս", Margin = new Thickness(0, 0, 8, 0) }; month.Click += (_, _) => showMonth();
        var week = new Button { Content = "Այս շաբաթ", Margin = new Thickness(0, 0, 8, 0) }; week.Click += (_, _) => showWeek();
        var day = new Button { Content = "Ընտրված օր", Margin = new Thickness(0, 0, 8, 0) }; day.Click += (_, _) => showDay();
        filters.Children.Add(month); filters.Children.Add(week); filters.Children.Add(day); root.Children.Add(filters);

        root.Children.Add(TileRow(
            ShowcaseMetric("Վաճառք",summary.Sales.SalesDisplay,"Ընտրված ժամանակահատված","▥","#0962FF"),
            ShowcaseMetric("Ինքնարժեք",summary.Sales.CostDisplay,"Վաճառված ապրանքների արժեք","◇","#12A875"),
            ShowcaseMetric("Շահույթ",summary.Sales.ProfitDisplay,"Վաճառք − ինքնարժեք","◷","#E99A16"),
            ShowcaseMetric("Կտրոններ",summary.Sales.ReceiptCount.ToString("N0"),"Միջին չեկ՝ "+A(summary.Sales.AverageReceipt),"▤","#8B5CF6")));

        var supplies = NewGrid("Պահեստ", "Մատակարարված ապրանք", "Տեսակ");
        foreach (var row in summary.Supplies.OrderByDescending(x => x.Amount))
            AddRow(supplies, row.Storage, A(row.Amount), row.IsProduction ? "Արտադրություն / Պատառիկ" : "Խանութ");
        AddRow(supplies, "Ընդհանուր", A(summary.SuppliedAmount), "");
        root.Children.Add(Card(new StackPanel { Children = { Text("Մատակարարված ապրանքներ՝ ըստ պահեստի", 16, FontWeights.SemiBold), Text($"Արտադրություն / Պատառիկ՝ {A(summary.ProductionSuppliedAmount)}", 13, null, BrushFor("#0F766E")), supplies } }));

        var debtGrid = NewGrid("Մատակարարված ապրանք", "Վճարված գումար", "Պարտքի փոփոխություն");
        AddRow(debtGrid, A(summary.SuppliedAmount), A(summary.SupplierPayments), SignedPlain(summary.DebtChange));
        root.Children.Add(Card(new StackPanel { Children = { Text("Մատակարարներ և պարտք", 16, FontWeights.SemiBold), debtGrid } }));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement Recommendations(DashboardSnapshot s, IReadOnlyList<EmployeeSupplierAction> employeeActions, IReadOnlyList<EmployeeTask> tasks, IReadOnlyList<EmployeeTaskAction> taskActions, IReadOnlyList<EmployeeIssue> employeeIssues)
    {
        var root = new StackPanel(); root.Children.Add(Text("AI առաջարկներ", 19, FontWeights.SemiBold));
        root.Children.Add(Text("Բոլոր առաջարկները բացատրելի են և նախատեսված են տնօրենի հաստատման համար։", 13, null, BrushFor("#64748B")));
        foreach (var r in s.Recommendations)
        {
            var panel = new StackPanel(); panel.Children.Add(Text($"{Icon(r.Severity)} {r.Title}", 16, FontWeights.SemiBold));
            panel.Children.Add(Text(r.Finding)); panel.Children.Add(Text($"Գործողություն՝ {r.SuggestedAction}", 14, FontWeights.SemiBold));
            panel.Children.Add(Text($"Հիմք՝ {r.Evidence}", 12, null, BrushFor("#64748B")));
            panel.Children.Add(new Button { Content = "Հաստատել առաջարկը", HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = true }); root.Children.Add(Card(panel));
        }
        root.Children.Add(EmployeeProblemsSection(s.Date, employeeActions));
        root.Children.Add(OtherEmployeeIssuesSection(s.Date, employeeIssues));
        root.Children.Add(EmployeeTasksSection(s.Date, tasks, taskActions));
        root.Children.Add(Section("🔔 Հուշումներ", new[] { "Այս բաժինը հետագայում կներառի վճարումների, պաշարների և ժամկետների ավտոմատ հիշեցումներ։" }));
        return new ScrollViewer { Content = root };
    }

    /// <summary>Owner approval queue for deviations reported by employees.</summary>
    public static UIElement Approvals(IReadOnlyList<PendingEmployeeOrderChange> changes, Action<Guid> approve, Action<Guid> reject, Action approveAll, Action<Guid> resolveSupplier, IReadOnlyList<string>? knownSuppliers = null)
    {
        var root = new StackPanel();
        root.Children.Add(Text("Հաստատումների կենտրոն", 19, FontWeights.SemiBold));
        root.Children.Add(Text("Այստեղ են աշխատակիցների նշած այն փաստացի տվյալները, որոնք տարբերվում են պլանից։ Հաստատումից հետո մատակարարի պլանը և պարտքի հաշվարկը թարմացվում են։", 13, null, BrushFor("#64748B")));
        root.Children.Add(TileRow(
            ShowcaseMetric("Սպասվող", changes.Count.ToString(), "Ուղարկված տողեր", "▤", "#0962FF"),
            ShowcaseMetric("Անունը ճշտելու", changes.Count(x => ApprovalWarnings.Unknown(x,knownSuppliers)).ToString(), "Պահանջում է վերանայում", "!", "#E5484D"),
            ShowcaseMetric("Հավանական կրկնում", changes.Count(x => ApprovalWarnings.Duplicate(x,changes)).ToString(), "Ստուգեք մինչև հաստատելը", "▣", "#E99A16")));

        if (changes.Count == 0)
        {
            root.Children.Add(Card(Text("✅ Սպասող հաստատումներ չկան։", 15, FontWeights.SemiBold, BrushFor("#166534"))));
            return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        var approveAllButton = new Button { Content = $"✓ Հաստատել բոլորը ({changes.Count})", Background = BrushFor("#166534"), Foreground = Brushes.White, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 14, 0, 12), HorizontalAlignment = HorizontalAlignment.Left };
        approveAllButton.Click += (_, _) => approveAll();
        root.Children.Add(approveAllButton);

        var grid = NewGrid("Ամսաթիվ", "Մատակարար", "Պլան", "Փաստացի", "Շեղում", "Աշխատակից", "");
        foreach (var item in changes.OrderBy(x => x.Date).ThenBy(x => x.Supplier))
        {
            var unknown = ApprovalWarnings.Unknown(item, knownSuppliers);
            var duplicate = ApprovalWarnings.Duplicate(item, changes);
            var planned = item.PlannedOrder + item.PlannedPayment + item.PlannedOldDebtPayment;
            var actual = item.ActualOrder + item.ActualPayment + item.ActualOldDebtPayment;
            var difference = actual - planned;
            var actions = new WrapPanel();
            var yes = new Button { Content = "Հաստատել", Background = BrushFor("#166534"), Foreground = Brushes.White, Padding = new Thickness(8, 3, 8, 3) };
            yes.Click += (_, _) => approve(item.Id);
            var no = new Button { Content = "Մերժել", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 3, 8, 3) };
            no.Click += (_, _) => reject(item.Id);
            actions.Children.Add(yes); actions.Children.Add(no);
            var edit = new Button { Content = "Խմբագրել", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 3, 8, 3), ToolTip = "Փոխել մատակարարի անունը և փաստացի գումարները" };
            if (item.Corrections is { Count: > 0 })
                edit.ToolTip = string.Join("\n", item.Corrections.Select(x =>
                    $"{x.At:dd.MM.yyyy HH:mm} · {x.EditedBy}\n{x.PreviousSupplier} → {x.Supplier}\nՊատվեր՝ {x.PreviousOrder:N2} → {x.Order:N2}, վճարում՝ {x.PreviousPayment:N2} → {x.Payment:N2}, հին պարտքի վճարում՝ {x.PreviousOldDebtPayment:N2} → {x.OldDebtPayment:N2}"));
            edit.Click += (_, _) => resolveSupplier(item.Id);
            actions.Children.Add(edit);
            if (item.RequiresSupplierReview)
            {
                yes.IsEnabled = false;
                var resolve = new Button { Content = "⚠ Ճշտել անունը", Margin = new Thickness(6, 0, 0, 0) };
                resolve.Click += (_, _) => resolveSupplier(item.Id);
                actions.Children.Add(resolve);
            }
            var name = Text(item.Supplier + (unknown ? "\n⚠ Անունը պետք է ճշտել" : "") + (duplicate ? "\n⚠ Հավանական կրկնում" : ""),
                14, unknown || duplicate ? FontWeights.SemiBold : FontWeights.Normal,
                unknown ? Brushes.Firebrick : duplicate ? Brushes.DarkOrange : BrushFor("#0F172A"));
            if (unknown) name.ToolTip = "Հավանական տարբերակներ՝ " + string.Join(", ", SupplierNameSuggestions.Find(item.Supplier, knownSuppliers ?? []));
            AddApprovalRow(grid, Text(item.Date.ToString("dd.MM.yyyy")), name,
                Text($"{A(item.PlannedOrder)} / {A(item.PlannedPayment)} / {A(item.PlannedOldDebtPayment)}"),
                Text($"{A(item.ActualOrder)} / {A(item.ActualPayment)} / {A(item.ActualOldDebtPayment)}"),
                Text(SignedPlain(difference)), Text(item.ReportedByName), actions);
        }
        root.Children.Add(Card(grid));
        root.Children.Add(Text("Ձևաչափը՝ պատվեր / նոր վճարում / հին պարտքի վճարում։", 12, null, BrushFor("#64748B")));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static UIElement EarlyWarningBlock(DashboardSnapshot snapshot, AvailableFundsBreakdown funds)
    {
        var negativeDay = snapshot.Forecast.OrderBy(x => x.Date).FirstOrDefault(x => x.ClosingBalance < 0m);
        var lowDay = snapshot.Forecast.OrderBy(x => x.Date).FirstOrDefault(x => x.ClosingBalance >= 0m && x.ClosingBalance < funds.Total * 0.10m);
        if (negativeDay is not null)
            return Card(new StackPanel { Children =
            {
                Text("🔴 Դրամական հոսքի վաղ նախազգուշացում", 16, FontWeights.SemiBold, BrushFor("#B91C1C")),
                Text($"{negativeDay.Date:dd.MM.yyyy}-ին կանխատեսվում է {A(negativeDay.ClosingBalance)} մնացորդ։"),
                Text("Առաջարկ՝ վերանայել ոչ պարտադիր գնումները և վճարումների հերթականությունը։", 13, FontWeights.SemiBold)
            }});
        if (lowDay is not null)
            return Card(new StackPanel { Children =
            {
                Text("🟡 Դրամական հոսքի դիտարկում", 16, FontWeights.SemiBold, BrushFor("#B45309")),
                Text($"{lowDay.Date:dd.MM.yyyy}-ին կանխատեսվող մնացորդը ցածր է՝ {A(lowDay.ClosingBalance)}։"),
                Text("Առաջարկ՝ պահել կանխիկի պահուստ և չավելացնել ոչ պարտադիր ծախսերը։", 13, FontWeights.SemiBold)
            }});
        return Card(Text("🟢 Առաջիկա կանխատեսվող դրամական հոսքը կառավարելի է։", 14, FontWeights.SemiBold, BrushFor("#166534")));
    }

    private static void AddApprovalRow(Grid grid, params UIElement[] cells)
    {
        var row = grid.RowDefinitions.Count; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is FrameworkElement element) element.Margin = new Thickness(0, 8, 5, 8);
            Grid.SetRow(cells[i], row); Grid.SetColumn(cells[i], i); grid.Children.Add(cells[i]);
        }
    }

    private static void AddApprovalRow(Grid grid, string date, string supplier, string planned, string actual, string difference, string employee, UIElement actions) =>
        AddApprovalRow(grid, Text(date), Text(supplier), Text(planned), Text(actual), Text(difference), Text(employee), actions);

    private static UIElement Metric(string title, string value, string hint, Action? click = null)
    {
        var card = Card(new StackPanel { Width = 190, Children = { Text(title, 13, null, BrushFor("#64748B")), Text(value, 25, FontWeights.Bold), Text(hint, 12, null, BrushFor("#64748B")) } });
        if (click is null) return card;
        var button = new Button { Content = card, Padding = new Thickness(0), Margin = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
        button.Click += (_, _) => click();
        return button;
    }
    private static UIElement CashDocumentSummaryBlock(CashDailySummary summary)
    {
        var grid = NewGrid("Վաճառք", "Կտրոններ", "Մատակարարների վճարում", "Պարտքի փոփոխություն", "Այլ ծախսեր", "Դրամարկղի փակում");
        AddRow(grid, A(summary.Sales), summary.ReceiptCount.ToString("N0"), A(summary.SupplierPayments), SignedPlain(summary.DebtChange), A(summary.OtherExpenses), A(summary.CashClosings));
        return Card(new StackPanel
        {
            Children =
            {
                Text($"Դրամարկղային ամփոփում — {summary.Date:dd.MM.yyyy}", 16, FontWeights.SemiBold),
                Text($"Ներմուծված փաստաթղթեր՝ {summary.DocumentCount}։ Մատակարարների վճարումը նույնքանով նվազեցնում է պարտքը։", 12, null, BrushFor("#64748B")),
                grid
            }
        });
    }
    private static UIElement SalesBlock(SalesSummary sales)
    {
        var grid = NewGrid("Վաճառք", "Ինքնարժեք", "Շահույթ", "Կտրոնների քանակ", "Միջին չեկ", "Փոփոխություններ");
        var changes = sales.SalesAvailable && sales.ComparisonAvailable
            ? $"Վաճառք՝ {SignedPlain(sales.SalesChange)}\nՇահույթ՝ {(sales.ProfitAvailable ? SignedPlain(sales.ProfitChange) : "Տվյալ չկա")}\nԿտրոններ՝ {sales.ReceiptChange:+#;-#;0}"
            : "Համեմատության տվյալները հասանելի չեն";
        AddRow(grid, sales.SalesDisplay, sales.CostDisplay, sales.ProfitDisplay, sales.ReceiptCount.ToString("N0"), A(sales.AverageReceipt), changes);
        return Card(new StackPanel { Children = { Text("📈 Վաճառքի ամփոփում", 16, FontWeights.SemiBold), Text("Այսօրվա ցուցանիշները՝ նախորդ օրվա համեմատ", 12, null, BrushFor("#64748B")), grid } });
    }
    private static Expander CollapsibleSection(string title, UIElement content) => new()
    {
        Header = Text($"⌄ {title}", 16, FontWeights.SemiBold, BrushFor("#0F766E")),
        Content = content,
        IsExpanded = false,
        Margin = new Thickness(0, 12, 0, 0),
        Padding = new Thickness(4)
    };

    private static Expander PaymentGroupExpander(string title, decimal amount, UIElement details)
    {
        var header = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 4, 0, 4) };
        header.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var total = Text(A(amount), 16, FontWeights.Bold, BrushFor("#0F766E")); total.Margin = new Thickness(22, 0, 0, 0); header.Children.Add(total);
        var more = Text("Մանրամասն", 12, null, BrushFor("#64748B")); more.Margin = new Thickness(18, 3, 0, 0); header.Children.Add(more);
        return new Expander { Header = header, Content = details, Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(4) };
    }
    private static Expander PaymentGroupExpander(string title, decimal planned, decimal actual, UIElement details)
    {
        var remaining = Math.Max(0m, planned - actual);
        var percent = planned == 0m ? (actual == 0m ? 0m : 100m) : Math.Min(100m, actual / planned * 100m);
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        header.Children.Add(Text(title, 15, FontWeights.SemiBold));
        var status = Text($"{percent:0}%", 16, FontWeights.Bold, percent >= 100m ? BrushFor("#0F766E") : BrushFor("#B45309")); status.Margin = new Thickness(20, 0, 0, 0); header.Children.Add(status);
        var summary = Text($"Պլան՝ {A(planned)} · Փաստացի՝ {A(actual)} · Մնաց՝ {A(remaining)}", 12, null, BrushFor("#475569")); summary.Margin = new Thickness(18, 3, 0, 0); header.Children.Add(summary);
        var body = new StackPanel();
        body.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = (double)percent, Height = 9, Margin = new Thickness(0, 5, 0, 10), Foreground = percent >= 100m ? BrushFor("#16A34A") : BrushFor("#D97706") });
        body.Children.Add(details);
        return new Expander { Header = header, Content = body, Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(4) };
    }
    private static UIElement SupplierPaymentDetails(IReadOnlyList<SupplierWeekPlanRow> rows, IReadOnlyList<PlannedPayment> extraPlans)
    {
        if (rows.Count == 0 && extraPlans.Count == 0) return Text("Այս օրվա մատակարարների վճարում գրանցված չէ։", 13, null, BrushFor("#64748B"));
        var grid = NewGrid("Մատակարար", "Պատվերից վճարում", "Հին պարտքի վճարում", "Ընդհանուր");
        foreach (var row in rows) AddRow(grid, row.Supplier, A(row.PaymentAmount), A(row.OldDebtPayment), A(row.PaymentAmount + row.OldDebtPayment));
        foreach (var plan in extraPlans) AddRow(grid, plan.Supplier, A(plan.Amount), "0 ֏", A(plan.Amount));
        return grid;
    }
    private static UIElement SupplierPaymentDetails(IReadOnlyList<SupplierWeekPlanRow> rows, IReadOnlyList<PlannedPayment> extraPlans, IReadOnlyList<CompletedPayment> completed)
    {
        var planned = rows.Select(x => (x.Supplier, Amount: x.PaymentAmount + x.OldDebtPayment))
            .Concat(extraPlans.Select(x => (x.Supplier, x.Amount)))
            .GroupBy(x => x.Supplier, StringComparer.OrdinalIgnoreCase)
            .Select(x => (Supplier: x.Key, Amount: x.Sum(y => y.Amount))).ToList();
        var names = planned.Select(x => x.Supplier).Concat(completed.Select(x => x.Recipient)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        if (names.Count == 0) return Text("Այս օրվա մատակարարների վճարում գրանցված չէ։", 13, null, BrushFor("#64748B"));
        var grid = NewGrid("Մատակարար", "Պլան", "Փաստացի", "Մնաց", "Կարգավիճակ");
        foreach (var name in names)
        {
            var expected = planned.Where(x => string.Equals(x.Supplier, name, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            var actual = completed.Where(x => string.Equals(x.Recipient, name, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            AddRow(grid, name, A(expected), A(actual), A(Math.Max(0m, expected - actual)), PaymentStatus(expected, actual));
        }
        return grid;
    }
    private static UIElement OtherExpenseDetails(IReadOnlyList<RequiredPaymentTemplate> required, IReadOnlyList<PlannedPayment> extraPlans)
    {
        if (required.Count == 0 && extraPlans.Count == 0) return Text("Այս օրվա այլ ծախս գրանցված չէ։", 13, null, BrushFor("#64748B"));
        var grid = NewGrid("Կատեգորիա", "Անվանում / ստացող", "Գումար", "Նշում");
        foreach (var item in required) AddRow(grid, item.Category, item.Name, A(item.Amount), string.IsNullOrWhiteSpace(item.Note) ? "—" : item.Note);
        foreach (var plan in extraPlans) AddRow(grid, "Այլ պլան", plan.Supplier, A(plan.Amount), plan.Reason);
        return grid;
    }
    private static UIElement OtherExpenseDetails(IReadOnlyList<RequiredPaymentTemplate> required, IReadOnlyList<PlannedPayment> extraPlans, IReadOnlyList<CompletedPayment> completed)
    {
        var planned = required.Select(x => (Category: x.Category, Name: x.Name, Amount: x.Amount))
            .Concat(extraPlans.Select(x => (Category: "Այլ ծախս", Name: x.Supplier, Amount: x.Amount))).ToList();
        var names = planned.Select(x => x.Name).Concat(completed.Select(x => x.Recipient)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        if (names.Count == 0) return Text("Այս օրվա այլ ծախս գրանցված չէ։", 13, null, BrushFor("#64748B"));
        var grid = NewGrid("Կատեգորիա", "Անվանում / ստացող", "Պլան", "Փաստացի", "Մնաց", "Կարգավիճակ");
        foreach (var name in names)
        {
            var found = planned.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            var expected = planned.Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            var actual = completed.Where(x => string.Equals(x.Recipient, name, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            AddRow(grid, found.Category ?? "Այլ ծախս", name, A(expected), A(actual), A(Math.Max(0m, expected - actual)), PaymentStatus(expected, actual));
        }
        return grid;
    }
    private static string PaymentStatus(decimal planned, decimal actual) =>
        planned == 0m && actual > 0m ? "Չպլանավորված" :
        actual >= planned && planned > 0m ? "✓ Կատարված" :
        actual > 0m ? "◐ Մասամբ կատարված" :
        planned > 0m ? "○ Չկատարված" : "—";
    private static UIElement CompletedPaymentDetails(IReadOnlyList<CompletedPayment> payments)
    {
        if (payments.Count == 0) return Text("Տվյալ օրվա կատարված վճարում չկա։", 13, null, BrushFor("#64748B"));
        var grid = NewGrid("Անվանում", "Գումար", "Նշում");
        foreach (var payment in payments) AddRow(grid, payment.Recipient, A(payment.Amount), string.IsNullOrWhiteSpace(payment.Note) ? "—" : payment.Note);
        return grid;
    }
    private static UIElement SalaryPaymentDetails(IReadOnlyList<SalaryPayment> payments, decimal planned)
    {
        var stack = new StackPanel();
        if (planned > 0m) stack.Children.Add(Text($"Կիրակիի պլանավորված աշխատավարձ՝ {A(planned)}", 13));
        if (payments.Count == 0)
            stack.Children.Add(Text("Այս օրը աշխատավարձի փաստացի վճարում չի գրանցվել։", 13));
        foreach (var payment in payments)
            stack.Children.Add(Text($"{payment.Employee} — {A(payment.Amount)} · շաբաթ {payment.WeekStart:dd.MM} · {payment.Note}", 13));
        return stack;
    }

    private static UIElement PaymentSummaryBlock(DashboardSnapshot snapshot, IReadOnlyList<CompletedPayment> completedPayments, IReadOnlyList<RequiredPaymentTemplate> requiredPayments)
    {
        var isPast = snapshot.Date < DateOnly.FromDateTime(DateTime.Today);
        var planned = isPast ? 0m : snapshot.Payments.Where(x => x.DueDate == snapshot.Date).Sum(x => x.Amount) + requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, snapshot.Date)).Sum(x => x.Amount);
        var actualRows = completedPayments.Where(x => x.PaidDate == snapshot.Date).ToList();
        var actual = actualRows.Sum(x => x.Amount);
        var variance = actual - planned;
        var label = variance switch { > 0 => "ավելի է վճարվել", < 0 => "պակաս է վճարվել", _ => "շեղում չկա" };
        var grid = NewGrid("Պլանավորված վճարումներ", "Կատարված վճարումներ", "Փաստացի գրանցումներ", "Շեղում");
        AddRow(grid, A(planned), A(actual), actualRows.Count.ToString(), $"{SignedPlain(variance)} · {label}");
        return Card(new StackPanel { Children = { Text("💳 Այսօրվա վճարումներ", 16, FontWeights.SemiBold), Text("Մատակարարների և այլ վճարների հանրագումար", 12, null, BrushFor("#64748B")), grid } });
    }
    private static UIElement PaymentSummaryBlock(DashboardSnapshot snapshot, IReadOnlyList<CompletedPayment> completedPayments, IReadOnlyList<RequiredPaymentTemplate> requiredPayments, IReadOnlyList<SupplierWeekPlanRow> supplierRows)
    {
        var supplierNames = supplierRows.Select(x => x.Supplier).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var supplierPlan = supplierRows.Sum(x => x.PaymentAmount + x.OldDebtPayment) + snapshot.Payments.Where(x => x.DueDate == snapshot.Date && supplierNames.Contains(x.Supplier)).Sum(x => x.Amount);
        var otherPlan = requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, snapshot.Date)).Sum(x => x.Amount) + snapshot.Payments.Where(x => x.DueDate == snapshot.Date && !supplierNames.Contains(x.Supplier)).Sum(x => x.Amount);
        var actualRows = completedPayments.Where(x => x.PaidDate == snapshot.Date).ToList();
        var supplierActual = actualRows.Where(x => supplierNames.Contains(x.Recipient)).Sum(x => x.Amount);
        var otherActual = actualRows.Where(x => !supplierNames.Contains(x.Recipient)).Sum(x => x.Amount);
        var planned = supplierPlan + otherPlan;
        var actual = supplierActual + otherActual;
        var remaining = Math.Max(0m, planned - actual);
        var percent = planned == 0m ? (actual == 0m ? 0m : 100m) : Math.Min(100m, actual / planned * 100m);
        var grid = NewGrid("Պլանավորված", "Փաստացի կատարված", "Կատարում", "Մնացել է");
        AddRow(grid, A(planned), A(actual), $"{percent:0}%", A(remaining));
        var breakdown = NewGrid("Բաժին", "Պլան", "Փաստացի", "Մնաց", "Կարգավիճակ");
        AddRow(breakdown, "Մատակարարների վճարներ", A(supplierPlan), A(supplierActual), A(Math.Max(0m, supplierPlan - supplierActual)), PaymentStatus(supplierPlan, supplierActual));
        AddRow(breakdown, "Այլ ծախսեր", A(otherPlan), A(otherActual), A(Math.Max(0m, otherPlan - otherActual)), PaymentStatus(otherPlan, otherActual));
        var body = new StackPanel();
        body.Children.Add(Text("💳 Այսօրվա վճարումների ընթացքը", 16, FontWeights.SemiBold));
        body.Children.Add(Text("Պլանավորված, փաստացի կատարված և մնացած վճարումների ամփոփում", 12, null, BrushFor("#64748B")));
        body.Children.Add(grid);
        body.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = (double)percent, Height = 10, Margin = new Thickness(0, 10, 0, 4), Foreground = percent >= 100m ? BrushFor("#16A34A") : BrushFor("#D97706") });
        body.Children.Add(Text($"Կատարման ընթացք՝ {percent:0}%", 12, FontWeights.SemiBold, percent >= 100m ? BrushFor("#0F766E") : BrushFor("#B45309")));
        body.Children.Add(breakdown);
        return Card(body);
    }

    private static IReadOnlyList<CompletedPayment> SupplierActualPaymentRows(DateOnly date, IReadOnlyList<SupplierWeekPlanRow> supplierRows, IReadOnlyList<CompletedPayment> completed, IReadOnlyList<EmployeeSupplierAction> employeeActions)
    {
        var result = completed.Where(x => supplierRows.Any(row => SupplierNamesMatch(row.Supplier, x.Recipient))).ToList();
        foreach (var row in supplierRows)
        {
            var hasCashRecord = result.Any(x => SupplierNamesMatch(x.Recipient, row.Supplier));
            var confirmed = employeeActions.Any(x => x.Date == date && SupplierNamesMatch(x.Supplier, row.Supplier) && x.Status == "Կատարված է");
            if (confirmed && !hasCashRecord && row.PaymentAmount + row.OldDebtPayment > 0m)
                result.Add(new CompletedPayment(row.Supplier, row.PaymentAmount + row.OldDebtPayment, date, "Աշխատակիցը հաստատել է ստացումը", "employee-confirmed"));
        }
        return result;
    }

    private static UIElement PaymentSummaryBlock(DashboardSnapshot snapshot, IReadOnlyList<CompletedPayment> completedPayments, IReadOnlyList<RequiredPaymentTemplate> requiredPayments, IReadOnlyList<SupplierWeekPlanRow> supplierRows, IReadOnlyList<EmployeeSupplierAction> employeeSupplierActions)
    {
        var supplierNames = supplierRows.Select(x => x.Supplier).ToList();
        var supplierPlan = supplierRows.Sum(x => x.PaymentAmount + x.OldDebtPayment) + snapshot.Payments.Where(x => x.DueDate == snapshot.Date && supplierNames.Contains(x.Supplier, StringComparer.OrdinalIgnoreCase)).Sum(x => x.Amount);
        var otherPlan = requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, snapshot.Date)).Sum(x => x.Amount) + snapshot.Payments.Where(x => x.DueDate == snapshot.Date && !supplierNames.Contains(x.Supplier, StringComparer.OrdinalIgnoreCase)).Sum(x => x.Amount);
        var completedForDate = completedPayments.Where(x => x.PaidDate == snapshot.Date).ToList();
        var supplierActual = SupplierActualPaymentRows(snapshot.Date, supplierRows, completedForDate, employeeSupplierActions).Sum(x => x.Amount);
        var otherActual = completedForDate.Where(x => !supplierRows.Any(row => SupplierNamesMatch(row.Supplier, x.Recipient))).Sum(x => x.Amount);
        var planned = supplierPlan + otherPlan;
        var actual = supplierActual + otherActual;
        var remaining = Math.Max(0m, planned - actual);
        var percent = planned == 0m ? (actual == 0m ? 0m : 100m) : Math.Min(100m, actual / planned * 100m);
        var grid = NewGrid("Պլանավորված", "Փաստացի կատարված", "Կատարում", "Մնացել է");
        AddRow(grid, A(planned), A(actual), $"{percent:0}%", A(remaining));
        var breakdown = NewGrid("Բաժին", "Պլան", "Փաստացի", "Մնաց", "Կարգավիճակ");
        AddRow(breakdown, "Մատակարարների վճարներ", A(supplierPlan), A(supplierActual), A(Math.Max(0m, supplierPlan - supplierActual)), PaymentStatus(supplierPlan, supplierActual));
        AddRow(breakdown, "Այլ ծախսեր", A(otherPlan), A(otherActual), A(Math.Max(0m, otherPlan - otherActual)), PaymentStatus(otherPlan, otherActual));
        var body = new StackPanel();
        body.Children.Add(Text("💳 Այսօրվա վճարումների ընթացքը", 16, FontWeights.SemiBold));
        body.Children.Add(Text("Պլանավորված, փաստացի կատարված և մնացած վճարումների ամփոփում", 12, null, BrushFor("#64748B")));
        body.Children.Add(grid);
        body.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = (double)percent, Height = 10, Margin = new Thickness(0, 10, 0, 4), Foreground = percent >= 100m ? BrushFor("#16A34A") : BrushFor("#D97706") });
        body.Children.Add(Text($"Կատարման ընթացք՝ {percent:0}%", 12, FontWeights.SemiBold, percent >= 100m ? BrushFor("#0F766E") : BrushFor("#B45309")));
        body.Children.Add(breakdown);
        return Card(body);
    }

    private static UIElement Section(string title, IEnumerable<string> items)
    {
        var stack = new StackPanel(); stack.Children.Add(Text(title, 16, FontWeights.SemiBold));
        foreach (var item in items) stack.Children.Add(new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(226,232,240)), BorderThickness = new Thickness(0,0,0,1), Padding = new Thickness(0,10,0,10), Child = Text(item) });
        return Card(stack);
    }
    private static Grid NewGrid(params string[] headers)
    {
        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) }; foreach (var _ in headers) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var band = new Border { Background = BrushFor("#EDF2F9"), CornerRadius = new CornerRadius(6), IsHitTestVisible = false };
        Grid.SetColumnSpan(band, Math.Max(1,headers.Length)); grid.Children.Add(band);
        for (var i=0;i<headers.Length;i++) { var t = Text(headers[i], 12, FontWeights.SemiBold, BrushFor("#475569")); t.Margin = new Thickness(7,13,7,13); Grid.SetColumn(t,i); grid.Children.Add(t); } return grid;
    }
    private static void AddRow(Grid grid, params string[] values)
    {
        var row = grid.RowDefinitions.Count; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowSurface(grid,row);
        for (var i=0; i<values.Length; i++) { var t = Text(values[i], 13); t.Margin = new Thickness(7,12,7,12); Grid.SetRow(t,row); Grid.SetColumn(t,i); grid.Children.Add(t); }
    }
    private static void AddEditableDeliveryRow(Grid grid, SupplierDeliveryPattern pattern, Action<SupplierDeliveryPattern, decimal> saveAmount)
    {
        var row = grid.RowDefinitions.Count; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var cells = new UIElement[]
        {
            Text(pattern.IsOneTime ? $"{pattern.Supplier}\n(միայն այս շաբաթ)" : pattern.Supplier, 13),
            Text(pattern.DeliveryCount.ToString(), 13), Text(A(pattern.AverageOrderAmount), 13),
            EditableAmountCell(pattern, saveAmount), Text(string.IsNullOrWhiteSpace(pattern.OwnerInstruction) ? "—" : pattern.OwnerInstruction, 13)
        };
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is FrameworkElement element) element.Margin = new Thickness(0, 8, 5, 8);
            Grid.SetRow(cells[i], row); Grid.SetColumn(cells[i], i); grid.Children.Add(cells[i]);
        }
    }
    private static UIElement EditableAmountCell(SupplierDeliveryPattern pattern, Action<SupplierDeliveryPattern, decimal> saveAmount)
    {
        var box = new TextBox { Text = pattern.SuggestedOrderAmount.ToString("0"), Width = 90, VerticalContentAlignment = VerticalAlignment.Center };
        var button = new Button { Content = "Պահ.", Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(5, 0, 0, 0) };
        button.Click += (_, _) =>
        {
            if (decimal.TryParse(box.Text.Replace(" ", ""), out var amount) && amount >= 0) saveAmount(pattern, amount);
            else MessageBox.Show("Գրեք ճիշտ գումար։", "Սխալ գումար", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { box, button } };
    }
    private static void AddEditableSupplierWeekRow(Grid grid, SupplierWeekPlanRow row, EmployeeSupplierAction? employeeAction, SupplierNote? latestNote, Action<SupplierWeekPlanRow, decimal, decimal, decimal, decimal> saveRow, Action<string> showSupplierStatus, Action<string> showSupplierDebtHistory, Action<SupplierWeekPlanRow, EmployeeSupplierAction?> editStatus, Action<SupplierWeekPlanRow> addNote, Action<SupplierWeekPlanRow> showAnalysis, List<Func<SupplierRowEdit?>> editors, IDictionary<string, string[]> drafts)
    {
        var gridRow = grid.RowDefinitions.Count; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var order = MoneyInput(row.OrderAmount); var payment = MoneyInput(row.PaymentAmount); var oldDebt = MoneyInput(row.OldDebtPayment); var debt = MoneyInput(row.Debt);
        var draftKey = $"{row.Date:yyyyMMdd}|{row.Supplier}";
        var inputs = new[] { order, payment, oldDebt, debt };
        if (drafts.TryGetValue(draftKey, out var pending) && pending.Length == inputs.Length)
            for (var i = 0; i < inputs.Length; i++) inputs[i].Text = pending[i];
        foreach (var input in inputs) input.TextChanged += (_, _) => drafts[draftKey] = inputs.Select(x => x.Text).ToArray();
        editors.Add(() =>
        {
            if (TryMoney(order, out var o) && TryMoney(payment, out var p) && TryMoney(oldDebt, out var h) && TryMoney(debt, out var d))
                return new SupplierRowEdit(row, o, p, h, d);
            order.Focus();
            return null;
        });
        var save = new Button { Content = "Պահպանել", Padding = new Thickness(10, 4, 10, 4), Background = BrushFor("#0F766E"), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };
        save.Click += (_, _) =>
        {
            if (TryMoney(order, out var orderValue) && TryMoney(payment, out var paymentValue) && TryMoney(oldDebt, out var oldDebtValue) && TryMoney(debt, out var debtValue)) saveRow(row, orderValue, paymentValue, oldDebtValue, debtValue);
            else MessageBox.Show("Գրեք ճիշտ գումարներ։", "Սխալ տվյալ", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        var change = row.OrderAmount - row.PaymentAmount - row.OldDebtPayment;
        var supplierButton = new Button { Content = row.Supplier, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = BrushFor("#0F766E"), Cursor = System.Windows.Input.Cursors.Hand };
        supplierButton.Click += (_, _) => showSupplierStatus(row.Supplier);
        var status = EmployeeStatusBadge(employeeAction);
        var statusText = employeeAction is null
            ? "Սեղմեք՝ կարգավիճակը ձեռքով փոխելու համար"
            : $"{employeeAction.Status}\n{employeeAction.Description}\nՓոփոխող՝ {employeeAction.ReportedByName}\nԺամանակ՝ {employeeAction.ReportedAt:dd.MM.yyyy HH:mm}\n\nՍեղմեք՝ կարգավիճակը ձեռքով փոխելու համար";
        var statusButton = new Button { Content = status, ToolTip = statusText, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
        statusButton.Click += (_, _) => editStatus(row, employeeAction);
        var shortNote = latestNote is null
            ? "＋ Նշում"
            : $"📌 {latestNote.Text[..Math.Min(latestNote.Text.Length, 24)]}{(latestNote.Text.Length > 24 ? "…" : string.Empty)}";
        var noteButton = new Button
        {
            Content = shortNote,
            ToolTip = latestNote is null ? "Նշում ավելացնել" : $"{latestNote.Text}\n{latestNote.Author} · {latestNote.CreatedAt:dd.MM.yyyy HH:mm}",
            Padding = new Thickness(7, 3, 7, 3),
            Background = latestNote is null ? Brushes.Transparent : BrushFor("#E0F2FE"),
            BorderBrush = latestNote is null ? BrushFor("#94A3B8") : BrushFor("#7DD3FC")
        };
        noteButton.Click += (_, _) => addNote(row);
        var analysis = new Button { Content = "Քարտ", ToolTip = "Բացել ստացման, վճարումների և վաճառքի վերլուծությունը", Padding = new Thickness(8, 3, 8, 3) };
        analysis.Click += (_, _) => showAnalysis(row);
        var debtHistory = new Button { Content = "Պատմ.", ToolTip = "Ցույց տալ պարտքի փոփոխությունների պատմությունը", Padding = new Thickness(8, 3, 8, 3) };
        debtHistory.Click += (_, _) => showSupplierDebtHistory(row.Supplier);
        supplierButton.Content = Text(row.Supplier, 13, FontWeights.SemiBold, BrushFor("#243B63"));
        supplierButton.HorizontalContentAlignment = HorizontalAlignment.Left;
        supplierButton.MaxWidth = 180;
        var debtCell = new StackPanel(); debtCell.Children.Add(debt);
        debtCell.Children.Add(Text(Signed(change), 10, null, change > 0 ? BrushFor("#B91C1C") : change < 0 ? BrushFor("#0F766E") : BrushFor("#475569")));
        var actions = new WrapPanel(); actions.Children.Add(analysis); actions.Children.Add(debtHistory); actions.Children.Add(save);
        analysis.Margin = debtHistory.Margin = new Thickness(0,0,4,3);
        noteButton.MaxWidth = 150;
        RowSurface(grid,gridRow);
        var cells = new UIElement[] { supplierButton, order, payment, oldDebt, debtCell, statusButton, noteButton, actions };
        for (var i = 0; i < cells.Length; i++) { if (cells[i] is FrameworkElement element) { element.Margin = new Thickness(6,10,6,10); element.VerticalAlignment = VerticalAlignment.Center; } Grid.SetRow(cells[i], gridRow); Grid.SetColumn(cells[i], i); grid.Children.Add(cells[i]); }
    }
    private static UIElement EmployeeStatusBadge(EmployeeSupplierAction? action)
    {
        var (label, background, foreground) = action?.Status switch
        {
            "Կատարված է" => ("✓ Հաստատված", "#DCFCE7", "#166534"),
            "Պատվերը գրանցվել է" => ("✓ Պատվիրված", "#DCFCE7", "#166534"),
            "Պատվիրված" => ("✓ Պատվիրված", "#DCFCE7", "#166534"),
            "Չի եկել" => ("✕ Չի եկել", "#FEE2E2", "#B91C1C"),
            "Մենեջերը չի եկել / պատվերը չի գրվել" => ("✕ Չի պատվիրվել", "#FEE2E2", "#B91C1C"),
            "Խնդիր" => ("⚠ Խնդիր կա", "#FEF3C7", "#92400E"),
            _ => ("● Սպասվում է", "#FFF6DF", "#98670C")
        };
        return new Border
        {
            Background = BrushFor(background), CornerRadius = new CornerRadius(5), Padding = new Thickness(7, 4, 7, 4),
            Child = Text(label, 12, FontWeights.SemiBold, BrushFor(foreground))
        };
    }

    private static UIElement EmployeeProblemsSection(DateOnly selectedDate, IReadOnlyList<EmployeeSupplierAction> actions)
    {
        var root = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        root.Children.Add(Text("⚠ Խնդիրներ՝ աշխատակիցներից", 17, FontWeights.SemiBold, BrushFor("#92400E")));
        root.Children.Add(Text($"{selectedDate:dd.MM.yyyy}-ի խնդիրները և մատակարարների պատմությունը", 12, null, BrushFor("#64748B")));

        var problems = actions.Where(x => x.Status is "Խնդիր" or "Չի եկել").OrderByDescending(x => x.ReportedAt).ToList();
        var suppliers = problems.Select(x => x.Supplier).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var filter = new ComboBox { Width = 280, Margin = new Thickness(0, 10, 0, 10) };
        filter.Items.Add("Բոլոր մատակարարները");
        foreach (var supplier in suppliers) filter.Items.Add(supplier);
        filter.SelectedIndex = 0;
        var list = new StackPanel();
        void Render()
        {
            list.Children.Clear();
            var chosen = filter.SelectedItem?.ToString();
            var source = chosen is null or "Բոլոր մատակարարները"
                ? problems.Where(x => x.Date == selectedDate)
                : problems.Where(x => SupplierNamesMatch(x.Supplier, chosen));
            var rows = source.OrderByDescending(x => x.Date).ThenByDescending(x => x.ReportedAt).ToList();
            if (rows.Count == 0)
            {
                list.Children.Add(Text(chosen is null or "Բոլոր մատակարարները" ? "Այս օրվա գրանցված խնդիրներ չկան։" : "Այս մատակարարի համար գրանցված խնդիրներ չկան։", 13, null, BrushFor("#64748B")));
                return;
            }
            foreach (var issue in rows)
            {
                var text = string.IsNullOrWhiteSpace(issue.Description) ? issue.Status : issue.Description;
                var item = new StackPanel();
                item.Children.Add(Text($"{issue.Date:dd.MM.yyyy} · {issue.Supplier}", 14, FontWeights.SemiBold, BrushFor("#92400E")));
                item.Children.Add(Text(text, 13));
                item.Children.Add(Text($"Նշել է՝ {issue.ReportedByName} · {issue.ReportedAt:HH:mm}", 11, null, BrushFor("#64748B")));
                list.Children.Add(new Border { Background = BrushFor("#FFFBEB"), BorderBrush = BrushFor("#FDE68A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 7), Child = item });
            }
        }
        filter.SelectionChanged += (_, _) => Render();
        Render();
        root.Children.Add(filter); root.Children.Add(list);
        return Card(root);
    }

    private static UIElement OtherEmployeeIssuesSection(DateOnly selectedDate, IReadOnlyList<EmployeeIssue> issues)
    {
        var root = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        var rows = issues.Where(x => x.Date == selectedDate).OrderByDescending(x => x.ReportedAt).ToList();
        root.Children.Add(Text("⚠ Այլ խնդիրներ", 17, FontWeights.SemiBold, BrushFor("#92400E")));
        root.Children.Add(Text("Աշխատակիցների ընդհանուր և առաջադրանքների հետ կապված խնդիրները", 12, null, BrushFor("#64748B")));
        if (rows.Count == 0)
        {
            root.Children.Add(Text("Այս օրվա համար այլ խնդիրներ չեն գրանցվել։", 13, null, BrushFor("#64748B")));
            return Card(root);
        }
        foreach (var issue in rows)
        {
            var item = new StackPanel();
            item.Children.Add(Text(issue.TaskDescription is null ? "Ընդհանուր խնդիր" : $"Առաջադրանք՝ {issue.TaskDescription}", 14, FontWeights.SemiBold, BrushFor("#92400E")));
            item.Children.Add(Text(issue.Description, 13));
            item.Children.Add(Text($"Նշել է՝ {issue.EmployeeName} · {issue.ReportedAt:HH:mm}", 11, null, BrushFor("#64748B")));
            if (!string.IsNullOrWhiteSpace(issue.DirectorResponse)) item.Children.Add(Text($"Տնօրենի պատասխան՝ {issue.DirectorResponse}", 12, FontWeights.SemiBold, BrushFor("#0F766E")));
            root.Children.Add(new Border { Background = BrushFor("#FFFBEB"), BorderBrush = BrushFor("#FDE68A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Margin = new Thickness(0, 8, 0, 0), Child = item });
        }
        return Card(root);
    }

    private static UIElement EmployeeTasksSection(DateOnly selectedDate, IReadOnlyList<EmployeeTask> tasks, IReadOnlyList<EmployeeTaskAction> actions)
    {
        var root = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        var rows = tasks.Where(x => x.Date == selectedDate).OrderBy(x => x.CreatedAt).ToList();
        root.Children.Add(Text("📋 Առաջադրանքներ", 17, FontWeights.SemiBold, BrushFor("#0F766E")));
        root.Children.Add(Text("Աշխատակիցների կողմից նշված կատարման և խնդիրների կարգավիճակները", 12, null, BrushFor("#64748B")));
        if (rows.Count == 0)
        {
            root.Children.Add(Text("Այս օրվա համար առաջադրանքներ չկան։", 13, null, BrushFor("#64748B")));
            return Card(root);
        }
        var grid = NewGrid("Առաջադրանք", "Կատարված", "Խնդիր", "Վերջին գրառում");
        foreach (var task in rows)
        {
            var reports = actions.Where(x => x.TaskId == task.Id).OrderByDescending(x => x.ReportedAt).ToList();
            var done = reports.Count(x => x.Status == "Կատարված է");
            var issues = reports.Where(x => x.Status == "Խնդիր").ToList();
            var last = reports.FirstOrDefault();
            var note = last is null ? "Սպասվում է" : $"{last.EmployeeName} · {last.Status}" + (string.IsNullOrWhiteSpace(last.Description) ? "" : $"\n{last.Description}");
            AddRow(grid, task.Description, done.ToString(), issues.Count.ToString(), note);
        }
        root.Children.Add(grid);
        return Card(root);
    }
    private static void AddRequiredPaymentRow(Grid grid, RequiredPaymentTemplate payment, Action<RequiredPaymentTemplate> edit, Action<RequiredPaymentTemplate> delete)
    {
        var row = grid.RowDefinitions.Count; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var editButton = new Button { Content = "Խմբ.", Padding = new Thickness(7, 3, 7, 3) }; editButton.Click += (_, _) => edit(payment);
        var deleteButton = new Button { Content = "Հեռ.", Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(4, 0, 0, 0) }; deleteButton.Click += (_, _) => delete(payment);
        actions.Children.Add(editButton); actions.Children.Add(deleteButton);
        var cells = new UIElement[] { Text(payment.Category), Text(payment.Name), Text(A(payment.Amount)), Text($"Ամսվա {payment.PaymentDay}-ին"), Text(RequiredPaymentRules.ScopeLabel(payment)), Text(string.IsNullOrWhiteSpace(payment.Note) ? "—" : payment.Note), actions };
        for (var i = 0; i < cells.Length; i++) { if (cells[i] is FrameworkElement element) element.Margin = new Thickness(0, 8, 5, 8); Grid.SetRow(cells[i], row); Grid.SetColumn(cells[i], i); grid.Children.Add(cells[i]); }
    }
    private static TextBox MoneyInput(decimal value) => new() { Text = value.ToString("0"), Width = 90, Height = 34, Padding = new Thickness(7,4,7,4), TextAlignment = TextAlignment.Right, VerticalContentAlignment = VerticalAlignment.Center };
    private static bool TryMoney(TextBox box, out decimal amount) => decimal.TryParse(box.Text.Replace(" ", ""), out amount) && amount >= 0;
    private static string Icon(Severity s) => s switch { Severity.Critical => "🔴", Severity.Warning => "🟡", _ => "🟢" };
    private static Brush BrushFor(string hex) => new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(hex));
    private static string Signed(decimal value) => value switch { > 0 => $"+{A(value)} (ավելացել է)", < 0 => $"{A(value)} (նվազել է)", _ => "0 ֏ (փոփոխություն չկա)" };
    private static string SignedPlain(decimal value) => value switch { > 0 => $"+{A(value)}", < 0 => $"{A(value)}", _ => "0 ֏" };
    private static string ChangeHint(decimal value, string suffix) => $"{SignedPlain(value)} · {suffix}";
    private static string ChangeHint(int value, string suffix) => $"{value:+#;-#;0} · {suffix}";
    private static SupplierWeekPlanRow ApplyKnownDebt(SupplierWeekPlanRow row, IReadOnlyList<Supplier> suppliers, IReadOnlyList<PartnerDebt> importedDebts)
    {
        if (row.Debt != 0) return row;
        var known = suppliers.FirstOrDefault(x => SupplierNamesMatch(x.Name, row.Supplier));
        if (known is not null && known.Debt != 0) return row with { Debt = known.Debt };
        var imported = importedDebts.FirstOrDefault(x => SupplierNamesMatch(x.Supplier, row.Supplier));
        return imported is null ? row : row with { Debt = imported.Amount };
    }
    private static bool SupplierNamesMatch(string first, string second)
    {
        var a = NormalizeSupplierName(first); var b = NormalizeSupplierName(second);
        if (a == b) return true;
        var aliases = new Dictionary<string, string>
        {
            ["մենթոս"] = "մենթոսսլավգրուպ",
            ["ֆիլիպմորիս"] = "ֆիլիպմորրիս"
        };
        if (aliases.TryGetValue(a, out var canonicalA)) a = canonicalA;
        if (aliases.TryGetValue(b, out var canonicalB)) b = canonicalB;
        if (a == b) return true;
        // Avoid treating a short generic word (for example "Աթենք") as a named legal entity.
        return Math.Min(a.Length, b.Length) >= 7 && (a.Contains(b) || b.Contains(a));
    }
    private static string NormalizeSupplierName(string value)
    {
        var normalized = new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        return normalized.Replace("սպը", "").Replace("փբը", "").Replace("հձ", "");
    }
    public static string ArmenianWeekdayLabel(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Երկուշաբթի", DayOfWeek.Tuesday => "Երեքշաբթի", DayOfWeek.Wednesday => "Չորեքշաբթի",
        DayOfWeek.Thursday => "Հինգշաբթի", DayOfWeek.Friday => "Ուրբաթ", DayOfWeek.Saturday => "Շաբաթ", _ => "Կիրակի"
    };
    private static string OldPaymentText(SupplierDailyMovement x) => x.OldDebtPayment == 0
        ? "0 ֏"
        : x.OldDebtDueDate is null ? PaymentText(x.OldDebtPayment, x.IsPlanned) : $"{PaymentText(x.OldDebtPayment, x.IsPlanned)}\n({x.OldDebtDueDate})";
    private static string PaymentText(SupplierDailyMovement x) => PaymentText(x.PaymentForOrder, x.IsPlanned);
    private static string PaymentText(decimal amount, bool isPlanned) => amount == 0 ? "0 ֏" : isPlanned ? $"{A(amount)}\n(պլան)" : A(amount);
    private static int DayOrder(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;
}
