namespace PatarikAIOS;

/// <summary>Boundary for HԾ Cloud. Replace the demo provider after API access is documented.</summary>
public interface IHtsDataProvider
{
    Task<DashboardSnapshot> GetSnapshotAsync(DateOnly date, CancellationToken cancellationToken = default);
}

public interface IBusinessSummaryProvider
{
    Task<BusinessSummary> GetBusinessSummaryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}

public interface IFundsMovementProvider
{
    Task<BankSalesBreakdown> GetNonCashSalesAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}

public interface IPurchasePlanningProvider
{
    Task<IReadOnlyList<PurchaseProposal>> GetPurchaseProposalsAsync(
        DateOnly stockDate,
        DateOnly deliveryDate,
        IReadOnlyList<string> scheduledSuppliers,
        IReadOnlyDictionary<string, int> supplierCoverageDays,
        CancellationToken cancellationToken = default);
}

public interface ISupplierSalesAnalysisProvider
{
    Task<IReadOnlyList<SupplierSalesAnalysis>> GetSupplierSalesAnalysisAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}

public sealed class DemoDataProvider : IHtsDataProvider, IBusinessSummaryProvider, IFundsMovementProvider, IPurchasePlanningProvider, ISupplierSalesAnalysisProvider
{
    public Task<DashboardSnapshot> GetSnapshotAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var today = date;
        var cash = new CashPosition(55_000m, 7_100m);
        var suppliers = new List<Supplier>
        {
            new("Դարոյնք", "Կոնֆետ / ձողիկ", 450_000m, today.AddDays(1), 92, "Նախորդ պարտքի մարում", "Պահել հարաբերությունը"),
            new("Լավաշ Դավթաշեն", "Լավաշ", 80_000m, today, 82, "Նույն օրը / նախորդ պարտք", "Ամենօրյա մատակարարում"),
            new("Ապաչի", "Խմիչքներ", 120_000m, today.AddDays(2), 61, "Նախորդ պարտքի մարում", "Կարելի է պլանավորել"),
            new("Սևանի պանիր", "Կաթնամթերք", 95_000m, today.AddDays(3), 76, "Նախորդ պարտքի մարում", "Պաշարը ստուգել")
        };
        var payments = new List<PlannedPayment>
        {
            new("Լավաշ Դավթաշեն", 20_000m, today, true, "Այսօրվա մատակարարում"),
            new("Դարոյնք", 100_000m, today.AddDays(1), true, "Բարձր առաջնահերթություն"),
            new("Ապաչի", 40_000m, today.AddDays(2), false, "Կարելի է տեղափոխել"),
            new("Աշխատավարձ", 300_000m, today.AddDays(3), true, "Հաստատուն ծախս")
        };
        // Demo structure mirrors the requested HԾ export columns. Replace these rows with API data.
        var movements = new List<SupplierDailyMovement>
        {
            new(today, "Գրանդ Տոբակո", "Ծխախոտ", 160_000m, 60_000m, 60_000m, 0m, null, "Այսօրվա պատվերը ամբողջությամբ վճարված է"),
            new(today, "Լավաշ Դավթաշեն", "Լավաշ", 12_000m, 1_500m, 1_500m, 0m, null, "Այսօրվա պատվերը ամբողջությամբ վճարված է"),
            new(today, "Դավիդով", "Ծխախոտ", 15_000m, 0m, 0m, 15_000m, today.AddDays(-1).ToString("dd.MM"), "Մարվել է հին պարտք"),
            new(today, "Ապաչի 2", "Խմիչքներ", 6_790m, 7_000m, 0m, 6_790m, today.AddDays(-2).ToString("dd.MM"), "Հին պարտքը փակվել է, նոր պատվերը մնացել է"),
            new(today.AddDays(1), "Ֆիլիպ Մորիս", "Ծխախոտ", 210_000m, 0m, 0m, 0m, null, "Պատվեր դեռ չի ստացվել"),
            new(today.AddDays(1), "Սևանի պանիր", "Կաթնամթերք", 95_000m, 0m, 0m, 0m, null, "Պատվեր դեռ չի ստացվել"),
            new(today.AddDays(1), "Աթենք կ/ֆ", "Կիսաֆաբրիկատ", 0m, 0m, 0m, 0m, null, "Պատվեր դեռ չի ստացվել")
        };
        var forecast = new List<CashForecast>();
        var balance = cash.Available;
        for (var i = 0; i < 7; i++)
        {
            var forecastDate = today.AddDays(i);
            var income = 180_000m;
            var outflow = payments.Where(x => x.DueDate == forecastDate).Sum(x => x.Amount);
            balance += income - outflow;
            forecast.Add(new CashForecast(forecastDate, income, outflow, balance));
        }
        var recommendations = new List<AiRecommendation>
        {
            new("Դարոյնքի վճարման պլան", Severity.Critical,
                "Վաղը մատակարարում է սպասվում, պարտքը՝ 450 000 ֏, առաջնահերթությունը՝ 92/100։",
                "Հաստատել 100 000 ֏ մասնակի վճարում՝ պահելով նվազագույն 50 000 ֏ պահուստ։",
                "Մատակարարի կարևորություն + հաջորդ մատակարարում", true),
            new("Ոչ պարտադիր ծախսեր", Severity.Warning,
                "3 օրից աշխատավարձի վճարում կա, կանխատեսվող մնացորդը նվազում է։",
                "Մինչև աշխատավարձը հետաձգել Ապաչիի 40 000 ֏ ոչ պարտադիր վճարումը։",
                "7-օրյա cash-flow կանխատեսում", true),
            new("Առավոտյան առաջադրանքներ", Severity.Info,
                "Այսօր սպասվում է Լավաշ Դավթաշենի մատակարարում։",
                "Պահեստի պատասխանատուին ուղարկել ընդունման և քանակի ստուգման առաջադրանք։",
                "Մատակարարման օրացույց", true)
        };
        var tasks = new List<DailyTask>
        {
            new("Պահեստ", "Ընդունել և ստուգել Լավաշ Դավթաշենի մուտքը", "12:00", "Սպասում է"),
            new("Ֆինանսներ", "Պատրաստել Դարոյնքի 100 000 ֏ վճարումը", "15:00", "Սպասում է հաստատման"),
            new("Վաճառող", "Գրանցել օրվա պակասներն ու հաճախորդների դիտարկումները", "20:00", "Սպասում է")
        };
        var sales = new SalesSummary(780_000m, 495_000m, 278, 720_000m, 246_000m, 259);
        return Task.FromResult(new DashboardSnapshot { Date = today, Cash = cash, Suppliers = suppliers, SupplierMovements = movements, Payments = payments, Forecast = forecast, Sales = sales, Recommendations = recommendations, Tasks = tasks });
    }

    public async Task<BusinessSummary> GetBusinessSummaryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var days = Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1).Select(startDate.AddDays).ToList();
        var snapshots = await Task.WhenAll(days.Select(day => GetSnapshotAsync(day, cancellationToken)));
        var sales = snapshots.Sum(x => x.Sales.SalesAmount);
        var cost = snapshots.Sum(x => x.Sales.CostAmount);
        var receipts = snapshots.Sum(x => x.Sales.ReceiptCount);
        return new BusinessSummary(
            startDate, endDate,
            new SalesSummary(sales, cost, receipts, 0m, 0m, 0),
            new[] { new EmployeeShiftSale("Վաճառող", "Հերթափոխ", sales, receipts) },
            new[] { new SupplyByStorage("Խանութ", 0m, false), new SupplyByStorage("Արտադրություն", 0m, true) },
            0m, 0m);
    }

    public Task<BankSalesBreakdown> GetNonCashSalesAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        Task.FromResult(BankSalesBreakdown.Empty);

    public Task<IReadOnlyList<PurchaseProposal>> GetPurchaseProposalsAsync(DateOnly stockDate, DateOnly deliveryDate, IReadOnlyList<string> scheduledSuppliers, IReadOnlyDictionary<string, int> supplierCoverageDays, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PurchaseProposal>>([]);

    public Task<IReadOnlyList<SupplierSalesAnalysis>> GetSupplierSalesAnalysisAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SupplierSalesAnalysis>>([]);
}

public sealed class AppServices(IHtsDataProvider provider)
{
    public IHtsDataProvider DataProvider { get; private set; } = provider;
    public void UseDataProvider(IHtsDataProvider provider) => DataProvider = provider;
}
