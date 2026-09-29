namespace PatarikAIOS;

/// <summary>Explicit Excel mode. Never mixes API and file sales.</summary>
public sealed class ExcelDataProvider(ExcelImportStore store) : IHtsDataProvider, IBusinessSummaryProvider,
    ISupplierSalesAnalysisProvider, ISupplierActivityProvider, IFundsMovementProvider, ICashDocumentProvider
{
    private List<ExcelSale> Sales(DateOnly start, DateOnly end) => store.Load().Batches.SelectMany(x => x.Sales).Where(x => x.Date >= start && x.Date <= end).ToList();
    private SalesSummary Summary(DateOnly start, DateOnly end)
    {
        var state = store.Load(); var rows = Sales(start, end);
        var covered = state.Batches.Where(x => x.Kind == "sales").SelectMany(x => x.Dates).ToHashSet();
        var full = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1).All(i => covered.Contains(start.AddDays(i)));
        var warnings = state.Batches.Where(x => x.Kind == "sales" && x.Dates.Any(d => d >= start && d <= end))
            .SelectMany(x => x.Warnings).Distinct().ToList();
        if (!full) warnings.Insert(0, "⚠ Ժամանակահատվածի բոլոր օրերի վաճառքները ներմուծված չեն։ Թվերը մասնակի են։");
        return new(rows.Sum(x => x.Sales), rows.Sum(x => x.Cost), rows.Select(x => (x.Date,x.Document)).Distinct().Count(),
            0, 0, 0, rows.Count > 0 || full, rows.Count > 0 || full, false,
            string.Join("\n", warnings.Take(12)) + (warnings.Count > 12 ? $"\nԵվս {warnings.Count - 12} զգուշացում․ տես ներմուծման պատմությունը։" : ""));
    }
    public Task<DashboardSnapshot> GetSnapshotAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DashboardSnapshot { Date = date, Cash = new(0,0), Sales = Summary(date,date),
            Suppliers = [], SupplierMovements = [], Payments = [], Forecast = [], Recommendations = [], Tasks = [] });

    public Task<BusinessSummary> GetBusinessSummaryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var state = store.Load(); var rows = Sales(startDate,endDate);
        var receipts = state.Batches.SelectMany(x => x.Receipts).Where(x => x.Date >= startDate && x.Date <= endDate).ToList();
        var paid = CashDocumentImportService.SupplierPaymentRows(state.Batches.SelectMany(x => x.Cash))
            .Where(x => x.Date >= startDate && x.Date <= endDate).Sum(x => x.Amount);
        return Task.FromResult(new BusinessSummary(startDate,endDate,Summary(startDate,endDate),
            rows.GroupBy(x => (x.Employee,x.Shift)).Select(g => new EmployeeShiftSale(g.Key.Employee,g.Key.Shift,g.Sum(x => x.Sales),g.Select(x => (x.Date,x.Document)).Distinct().Count())).ToList(),
            receipts.GroupBy(x => x.Storage).Select(g => new SupplyByStorage(g.Key,g.Sum(x => x.Amount),g.Key.Contains("Արտադր"))).ToList(),
            paid,receipts.Sum(x => x.Amount)-paid));
    }
    public Task<IReadOnlyList<SupplierSalesAnalysis>> GetSupplierSalesAnalysisAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SupplierSalesAnalysis>>(Sales(startDate,endDate)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Supplier) ? "Մատակարարը չճշտված" : x.Supplier)
            .Select(g => new SupplierSalesAnalysis(g.Key,g.Sum(x => x.Sales),g.Sum(x => x.Cost),g.Sum(x => x.Quantity),
                g.Select(x => x.ProductCode).Distinct().Count(),g.Select(x => x.Storage).Distinct().Count())).ToList());

    public Task<IReadOnlyList<SupplierActivityLine>> GetSupplierActivityAsync(string supplier, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var state = store.Load();
        var receipts = state.Batches.SelectMany(x => x.Receipts).Where(x => x.Supplier == supplier && x.Date >= startDate && x.Date <= endDate).ToList();
        var paid = CashDocumentImportService.SupplierPaymentRows(state.Batches.SelectMany(x => x.Cash)).Where(x => x.Recipient == supplier && x.Date >= startDate && x.Date <= endDate).ToList();
        return Task.FromResult<IReadOnlyList<SupplierActivityLine>>(receipts.Select(x => x.Date).Concat(paid.Select(x => x.Date)).Distinct().Order()
            .Select(d => new SupplierActivityLine(d, receipts.Where(x => x.Date == d).Sum(x => x.Amount),
                paid.Where(x => x.Date == d).Sum(x => x.Amount),0,
                string.Join(", ",receipts.Where(x => x.Date == d).Select(x => x.Document).Concat(paid.Where(x => x.Date == d).Select(x => x.DocumentNumber))),
                "Excel․ վճարման բաժանումը նոր/հին պարտքի միջև ֆայլում նշված չէ")).ToList());
    }
    public Task<BankSalesBreakdown> GetNonCashSalesAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BankSalesBreakdown(store.Load().NonCash.Where(x => x.Date >= startDate && x.Date <= endDate)
            .GroupBy(x => x.Date).Sum(g => g.Last().Amount),0,0));

    public Task<IReadOnlyList<CashDocumentRecord>> GetCashDocumentsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var state = store.Load(); var rows = state.Batches.SelectMany(x => x.Cash).Where(x => x.Date >= startDate && x.Date <= endDate).ToList();
        var result = rows.Where(x => !x.Type.Contains("Վաճառք")).ToList();
        foreach (var day in rows.Where(x => x.Type.Contains("Վաճառք")).GroupBy(x => x.Date))
        {
            var nonCash = state.NonCash.LastOrDefault(x => x.Date == day.Key);
            // Full sales are provisionally cash until a non-cash split is entered.
            // The UI explicitly warns about the missing split.
            if (nonCash is not null && nonCash.Amount > day.Sum(x => x.Amount)) throw new InvalidOperationException($"{day.Key:dd.MM}․ անկանխիկ վաճառքը գերազանցում է ամբողջ վաճառքը։");
            result.Add(new(day.Key,"EXCEL-CASH-"+day.Key.ToString("yyyyMMdd"),"ecr-cash-sales",
                day.Sum(x => x.Amount)-(nonCash?.Amount ?? 0m),"Excel ամբողջ վաճառք − նշված անկանխիկ","Վաճառք","0001","",""));
        }
        return Task.FromResult<IReadOnlyList<CashDocumentRecord>>(result);
    }
}
