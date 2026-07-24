using System.Windows.Controls;
using System.Windows.Media;

namespace PatarikAIOS;

public static class Views
{
    private static TextBlock Text(string value, double size = 14, FontWeight? weight = null, Brush? color = null) => new()
    { Text = value, FontSize = size, FontWeight = weight ?? FontWeights.Normal, Foreground = color ?? BrushFor("#1E293B"), TextWrapping = TextWrapping.Wrap };

    private static Border Card(UIElement child) => new() { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(18), Margin = new Thickness(0, 0, 14, 14), Child = child, BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)), BorderThickness = new Thickness(1) };
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
        Action openApprovals)
    {
        var root = new StackPanel();
        root.Children.Add(Text("CEO Dashboard — օրվա վերահսկման կենտրոն", 19, FontWeights.SemiBold));
        var row = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        row.Children.Add(Metric("Հասանելի միջոցներ", A(funds.Total), $"Կանխիկ՝ {A(funds.Cash)} · Բանկ՝ {A(funds.Bank)}", openFunds));
        // Use the same rule as the Payments page: supplier rows for the selected
        // day + API/manual plans + recurring non-supplier expenses.
        var plannedPayments = supplierRows.Sum(x => x.PaymentAmount + x.OldDebtPayment) +
            s.Payments.Where(p => p.DueDate == s.Date).Sum(p => p.Amount) +
            requiredPayments.Where(p => RequiredPaymentRules.AppliesOn(p, s.Date)).Sum(p => p.Amount);
        row.Children.Add(Metric("Այսօրվա վճարումներ", A(plannedPayments), "Սեղմեք՝ վճարումների ցանկը տեսնելու համար", openPayments));
        row.Children.Add(Metric("Այսօրվա վաճառք", A(s.Sales.SalesAmount), ChangeHint(s.Sales.SalesChange, "նախորդ օրվա համեմատ")));
        row.Children.Add(Metric("Շահույթ", A(s.Sales.Profit), ChangeHint(s.Sales.ProfitChange, "նախորդ օրվա համեմատ")));
        row.Children.Add(Metric("Կրիտիկական ռիսկեր", s.Recommendations.Count(x => x.Severity == Severity.Critical).ToString(), "Սեղմեք՝ ռիսկերը տեսնելու համար", openRisks));
        row.Children.Add(Metric("Կտրոններ", s.Sales.ReceiptCount.ToString("N0"), ChangeHint(s.Sales.ReceiptChange, "նախորդ օրվա համեմատ")));
        row.Children.Add(Metric("Սպասվող հաստատումներ", pendingApprovals.ToString(), pendingApprovals == 0 ? "Նոր հաստատում չկա" : "Սեղմեք՝ փոփոխությունները տեսնելու համար", openApprovals));
        root.Children.Add(row);
        root.Children.Add(PaymentSummaryBlock(s, completedPayments, requiredPayments, supplierRows, employeeSupplierActions));
        root.Children.Add(EarlyWarningBlock(s, funds));
        if (cashSummary is not null) root.Children.Add(CashDocumentSummaryBlock(cashSummary));
        root.Children.Add(SalesBlock(s.Sales));
        root.Children.Add(Section("⚠ AI ուշադրության կենտրոն", s.Recommendations.Take(3).Select(r => $"{Icon(r.Severity)}  {r.Title}\n{r.Finding}\nԱռաջարկ՝ {r.SuggestedAction}")));
        root.Children.Add(Section("✅ Այսօրվա առաջադրանքներ", s.Tasks.Select(t => $"{t.Owner} · {t.Task}\nԺամկետ՝ {t.Deadline} · {t.Status}")));
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement Finance(DashboardSnapshot s)
    {
        var root = new StackPanel(); root.Children.Add(Text("Ֆինանսական կենտրոն", 19, FontWeights.SemiBold));
        root.Children.Add(Section("💰 Այս պահի դիրք", new[] { $"Դրամարկղ՝ {A(s.Cash.Cash)}", $"Բանկ՝ {A(s.Cash.Bank)}", $"Ընդհանուր հասանելի՝ {A(s.Cash.Available)}" }));
        var grid = NewGrid("Ամսաթիվ", "Սպասվող մուտք", "Սպասվող ելք", "Օրվա վերջի կանխատեսում");
        foreach (var f in s.Forecast) AddRow(grid, f.Date.ToString("dd.MM"), A(f.ExpectedIncome), A(f.ExpectedOutflow), A(f.ClosingBalance));
        root.Children.Add(Card(new StackPanel { Children = { Text("📈 7-օրյա cash-flow կանխատեսում", 16, FontWeights.SemiBold), grid } }));
        return new ScrollViewer { Content = root };
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

    public static UIElement Suppliers(IReadOnlyList<SupplierWeekPlanRow> rows, IReadOnlyList<Supplier> supplierDebts, IReadOnlyList<PartnerDebt> importedDebts, IReadOnlyList<EmployeeSupplierAction> employeeActions, IReadOnlyList<SupplierStatusChange> statusChanges, Action<SupplierWeekPlanRow, decimal, decimal, decimal, decimal> saveRow, Action<string> showSupplierStatus, Action<SupplierWeekPlanRow, EmployeeSupplierAction?> editStatus)
    {
        var root = new StackPanel();
        root.Children.Add(Text("Մատակարարների շաբաթական գրաֆիկ", 19, FontWeights.SemiBold));
        root.Children.Add(Text("01.07.2026–31.12.2026 · Ընտրված օրվա մատակարարման գրաֆիկը և գումարները։", 13, null, BrushFor("#64748B")));
        foreach (var day in rows.GroupBy(x => x.Date).OrderBy(x => x.Key))
        {
            root.Children.Add(Text($"{ArmenianWeekdayLabel(day.Key.DayOfWeek)} · {day.Key:dd.MM.yyyy}", 16, FontWeights.SemiBold, BrushFor("#0F766E")));
            var grid = NewGrid("Մատակարար", "Պատվեր", "Վճարում", "Հին թվի վճարում", "Ընթացիկ պարտք", "Պարտքի փոփոխություն", "Կարգավիճակ", "");
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
                if (manualChange is not null)
                {
                    var explanation = $"Ձեռքով փոփոխվել է {manualChange.ChangedAt:dd.MM.yyyy HH:mm}-ին։ Նախորդ կարգավիճակ՝ {manualChange.PreviousStatus}.";
                    if (!string.IsNullOrWhiteSpace(manualChange.Note)) explanation += $" Նշում՝ {manualChange.Note}";
                    currentAction = new EmployeeSupplierAction(row.Date, row.Supplier, manualChange.NewStatus, explanation, "owner", manualChange.ChangedBy, manualChange.ChangedAt);
                }
                AddEditableSupplierWeekRow(grid, ApplyKnownDebt(row, supplierDebts, importedDebts), currentAction, saveRow, showSupplierStatus, editStatus);
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
            root.Children.Add(Card(new StackPanel { Children = { grid, new Separator { Margin = new Thickness(0, 12, 0, 10) }, summary } }));
        }
        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public static UIElement Payments(DashboardSnapshot s, IReadOnlyList<CompletedPayment> completedPayments, IReadOnlyList<RequiredPaymentTemplate> requiredPayments, IReadOnlyList<SupplierWeekPlanRow> supplierRows, IReadOnlyList<EmployeeSupplierAction> employeeSupplierActions, Action<RequiredPaymentTemplate> editPayment, Action<RequiredPaymentTemplate> deletePayment)
    {
        var isPast = s.Date < DateOnly.FromDateTime(DateTime.Today);
        var root = new StackPanel(); root.Children.Add(Text("Վճարումների պլան", 19, FontWeights.SemiBold));
        root.Children.Add(Text($"Ընտրված ամսաթիվ՝ {s.Date:dd.MM.yyyy}. Ստորև ցուցադրված են միայն այդ օրվա պլանավորված և փաստացի վճարումները։", 13, null, BrushFor("#64748B")));
        var supplierNames = supplierRows.Select(x => x.Supplier).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var completedForDate = completedPayments.Where(x => x.PaidDate == s.Date).ToList();
        var supplierCompleted = SupplierActualPaymentRows(s.Date, supplierRows, completedForDate, employeeSupplierActions);
        var otherCompleted = completedForDate.Where(x => !supplierRows.Any(row => SupplierNamesMatch(row.Supplier, x.Recipient))).ToList();
        var supplierItems = supplierRows.Where(x => x.PaymentAmount > 0 || x.OldDebtPayment > 0).ToList();
        var supplierPlanItems = s.Payments.Where(x => x.DueDate == s.Date && supplierNames.Contains(x.Supplier)).ToList();
        var supplierPlannedTotal = supplierItems.Sum(x => x.PaymentAmount + x.OldDebtPayment) + supplierPlanItems.Sum(x => x.Amount);
        var supplierActualTotal = supplierCompleted.Sum(x => x.Amount);
        var otherRequired = requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, s.Date)).ToList();
        var otherPlanItems = s.Payments.Where(x => x.DueDate == s.Date && !supplierNames.Contains(x.Supplier)).ToList();
        var otherPlannedTotal = otherRequired.Sum(x => x.Amount) + otherPlanItems.Sum(x => x.Amount);
        var otherActualTotal = otherCompleted.Sum(x => x.Amount);
        var daySummary = new StackPanel();
        daySummary.Children.Add(PaymentGroupExpander("Մատակարարների վճարներ", supplierPlannedTotal, supplierActualTotal, SupplierPaymentDetails(supplierItems, supplierPlanItems, supplierCompleted)));
        daySummary.Children.Add(PaymentGroupExpander("Այլ ծախսեր", otherPlannedTotal, otherActualTotal, OtherExpenseDetails(otherRequired, otherPlanItems, otherCompleted)));
        root.Children.Add(Text("Պլանավորված և փաստացի կատարված վճարումների համեմատություն։", 13, null, BrushFor("#64748B")));
        var grid = NewGrid("Ամսաթիվ", "Ստացող", "Գումար", "Կարգ", "Պատճառ");
        // The old full list is intentionally retained in memory for the monthly plan below.
        // The owner sees the selected day's two payment groups instead.
        root.Children.Add(Card(daySummary));

        root.Children.Add(Text("Պարտադիր վճարների բազա", 16, FontWeights.SemiBold, BrushFor("#0F766E")));
        var requiredGrid = NewGrid("Կատեգորիա", "Անվանում / ստացող", "Գումար", "Վճարման օր", "Կրկնում", "Նշում", "");
        foreach (var item in requiredPayments.Where(x => !isPast && x.IsActive && RequiredPaymentRules.AppliesOn(x, s.Date)).OrderBy(x => x.PaymentDay))
            AddRequiredPaymentRow(requiredGrid, item, editPayment, deletePayment);
        if (requiredPayments.All(x => !x.IsActive || !RequiredPaymentRules.AppliesOn(x, s.Date)))
            AddRow(requiredGrid, "—", "Այս օրվա համար պարտադիր վճարում չկա", "", "", "", "", "");
        root.Children.Add(Card(requiredGrid));

        root.Children.Add(Text("Ընտրված ամսվա պարտադիր վճարումների բազա", 16, FontWeights.SemiBold, BrushFor("#0F766E")));
        root.Children.Add(Text("Այստեղից կարող եք խմբագրել կամ հեռացնել ցանկացած վճարում՝ առանց այլ օր ընտրելու։", 12, null, BrushFor("#64748B")));
        var monthlyGrid = NewGrid("Կատեգորիա", "Անվանում / ստացող", "Գումար", "Վճարման օր", "Կրկնում", "Նշում", "");
        var monthItems = requiredPayments.Where(x => RequiredPaymentRules.AppliesInMonth(x, s.Date)).OrderBy(x => x.PaymentDay).ThenBy(x => x.Name).ToList();
        foreach (var item in monthItems) AddRequiredPaymentRow(monthlyGrid, item, editPayment, deletePayment);
        if (!monthItems.Any()) AddRow(monthlyGrid, "—", "Բազայում վճարում չկա", "", "", "", "", "");
        root.Children.Add(Card(monthlyGrid));

        root.Children.Add(Text("Ամսական վճարումների գրաֆիկ", 16, FontWeights.SemiBold, BrushFor("#0F766E")));
        var scheduleGrid = NewGrid("Վճարման օր", "Վճարումներ", "Ընդհանուր գումար");
        foreach (var group in requiredPayments.Where(x => !isPast && RequiredPaymentRules.AppliesOn(x, s.Date)).GroupBy(x => x.PaymentDay).OrderBy(x => x.Key))
            AddRow(scheduleGrid, group.Key.ToString(), string.Join(", ", group.Select(x => x.Name)), A(group.Sum(x => x.Amount)));
        root.Children.Add(Card(scheduleGrid));
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
        metrics.Children.Add(Metric("Ինքնարժեք", A(totalCost), "Ըստ վաճառված ապրանքների"));
        metrics.Children.Add(Metric("Շահույթ", A(totalSales - totalCost), "Մատակարարային կապով"));
        root.Children.Add(metrics);

        var grid = NewGrid("Մատակարար", "Վաճառք", "Ինքնարժեք", "Շահույթ", "Քանակ", "Ապրանք", "Պահեստ");
        foreach (var row in rows)
            AddRow(grid, row.Supplier, A(row.SalesAmount), A(row.CostAmount), A(row.Profit), row.Quantity.ToString("N2"), row.ProductCount.ToString(), row.StorageCount.ToString());
        root.Children.Add(Card(new StackPanel { Children = { Text("Մանրամասն", 16, FontWeights.SemiBold), grid } }));
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
        root.Children.Add(Text($"Ժամանակահատված՝ {filterDescription}", 13, null, BrushFor("#64748B")));

        var filters = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 16) };
        var month = new Button { Content = "Այս ամիս", Margin = new Thickness(0, 0, 8, 0) }; month.Click += (_, _) => showMonth();
        var week = new Button { Content = "Այս շաբաթ", Margin = new Thickness(0, 0, 8, 0) }; week.Click += (_, _) => showWeek();
        var day = new Button { Content = "Ընտրված օր", Margin = new Thickness(0, 0, 8, 0) }; day.Click += (_, _) => showDay();
        filters.Children.Add(month); filters.Children.Add(week); filters.Children.Add(day); root.Children.Add(filters);

        var salesGrid = NewGrid("Վաճառք", "Ինքնարժեք", "Շահույթ", "Կտրոններ", "Միջին չեկ");
        AddRow(salesGrid, A(summary.Sales.SalesAmount), A(summary.Sales.CostAmount), A(summary.Sales.Profit), summary.Sales.ReceiptCount.ToString("N0"), A(summary.Sales.AverageReceipt));
        root.Children.Add(Card(new StackPanel { Children = { Text("Վաճառք և շահութաբերություն", 16, FontWeights.SemiBold), salesGrid } }));

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
    public static UIElement Approvals(IReadOnlyList<PendingEmployeeOrderChange> changes, Action<Guid> approve, Action<Guid> reject, Action approveAll)
    {
        var root = new StackPanel();
        root.Children.Add(Text("Հաստատումների կենտրոն", 19, FontWeights.SemiBold));
        root.Children.Add(Text("Այստեղ են աշխատակիցների նշած այն փաստացի տվյալները, որոնք տարբերվում են պլանից։ Հաստատումից հետո մատակարարի պլանը և պարտքի հաշվարկը թարմացվում են։", 13, null, BrushFor("#64748B")));

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
            var planned = item.PlannedOrder + item.PlannedPayment + item.PlannedOldDebtPayment;
            var actual = item.ActualOrder + item.ActualPayment + item.ActualOldDebtPayment;
            var difference = actual - planned;
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            var yes = new Button { Content = "Հաստատել", Background = BrushFor("#166534"), Foreground = Brushes.White, Padding = new Thickness(8, 3, 8, 3) };
            yes.Click += (_, _) => approve(item.Id);
            var no = new Button { Content = "Մերժել", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 3, 8, 3) };
            no.Click += (_, _) => reject(item.Id);
            actions.Children.Add(yes); actions.Children.Add(no);
            AddApprovalRow(grid,
                item.Date.ToString("dd.MM.yyyy"), item.Supplier,
                $"{A(item.PlannedOrder)} / {A(item.PlannedPayment)} / {A(item.PlannedOldDebtPayment)}",
                $"{A(item.ActualOrder)} / {A(item.ActualPayment)} / {A(item.ActualOldDebtPayment)}",
                SignedPlain(difference), item.ReportedByName, actions);
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
        var changes = $"Վաճառք՝ {SignedPlain(sales.SalesChange)}\nՇահույթ՝ {SignedPlain(sales.ProfitChange)}\nԿտրոններ՝ {sales.ReceiptChange:+#;-#;0}";
        AddRow(grid, A(sales.SalesAmount), A(sales.CostAmount), A(sales.Profit), sales.ReceiptCount.ToString("N0"), A(sales.AverageReceipt), changes);
        return Card(new StackPanel { Children = { Text("📈 Վաճառքի ամփոփում", 16, FontWeights.SemiBold), Text("Այսօրվա ցուցանիշները՝ նախորդ օրվա համեմատ", 12, null, BrushFor("#64748B")), grid } });
    }
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
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); for (var i=0;i<headers.Length;i++) { var t = Text(headers[i], 12, FontWeights.SemiBold, BrushFor("#475569")); Grid.SetColumn(t,i); grid.Children.Add(t); } return grid;
    }
    private static void AddRow(Grid grid, params string[] values)
    {
        var row = grid.RowDefinitions.Count; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i=0; i<values.Length; i++) { var t = Text(values[i], 13); t.Margin = new Thickness(0,8,5,8); Grid.SetRow(t,row); Grid.SetColumn(t,i); grid.Children.Add(t); }
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
    private static void AddEditableSupplierWeekRow(Grid grid, SupplierWeekPlanRow row, EmployeeSupplierAction? employeeAction, Action<SupplierWeekPlanRow, decimal, decimal, decimal, decimal> saveRow, Action<string> showSupplierStatus, Action<SupplierWeekPlanRow, EmployeeSupplierAction?> editStatus)
    {
        var gridRow = grid.RowDefinitions.Count; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var order = MoneyInput(row.OrderAmount); var payment = MoneyInput(row.PaymentAmount); var oldDebt = MoneyInput(row.OldDebtPayment); var debt = MoneyInput(row.Debt);
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
        var cells = new UIElement[] { supplierButton, order, payment, oldDebt, debt, Text(Signed(change), 13, null, change > 0 ? BrushFor("#B91C1C") : change < 0 ? BrushFor("#0F766E") : BrushFor("#475569")), statusButton, save };
        for (var i = 0; i < cells.Length; i++) { if (cells[i] is FrameworkElement element) element.Margin = new Thickness(0, 8, 5, 8); Grid.SetRow(cells[i], gridRow); Grid.SetColumn(cells[i], i); grid.Children.Add(cells[i]); }
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
            _ => ("Սպասվում է", "#F1F5F9", "#475569")
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
    private static TextBox MoneyInput(decimal value) => new() { Text = value.ToString("0"), Width = 90, VerticalContentAlignment = VerticalAlignment.Center };
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
