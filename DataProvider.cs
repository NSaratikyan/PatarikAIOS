namespace PatarikAIOS;

/// <summary>Read-only boundary for HԾ Cloud data.</summary>
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

/// <summary>Read-only cash-document feed used to reconcile cash desks by day.</summary>
public interface ICashDocumentProvider
{
    Task<IReadOnlyList<CashDocumentRecord>> GetCashDocumentsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}

public interface IPurchasePlanningProvider
{
    Task<IReadOnlyList<PurchaseProposal>> GetPurchaseProposalsAsync(DateOnly stockDate, DateOnly deliveryDate,
        IReadOnlyList<string> scheduledSuppliers, IReadOnlyDictionary<string, int> supplierCoverageDays,
        CancellationToken cancellationToken = default);
}

public interface ISupplierSalesAnalysisProvider
{
    Task<IReadOnlyList<SupplierSalesAnalysis>> GetSupplierSalesAnalysisAsync(DateOnly startDate, DateOnly endDate,
        CancellationToken cancellationToken = default);
}

public interface ISupplierActivityProvider
{
    Task<IReadOnlyList<SupplierActivityLine>> GetSupplierActivityAsync(string supplier, DateOnly startDate, DateOnly endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe fallback used only before HԾ API is configured. It deliberately returns
/// no invented sales, money, supplier, payment, task, or AI data.
/// </summary>
public sealed class EmptyDataProvider : IHtsDataProvider, IBusinessSummaryProvider, IFundsMovementProvider, ICashDocumentProvider,
    IPurchasePlanningProvider, ISupplierSalesAnalysisProvider, ISupplierActivityProvider
{
    public Task<DashboardSnapshot> GetSnapshotAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DashboardSnapshot
        {
            Date = date,
            Cash = new CashPosition(0m, 0m),
            Suppliers = [],
            SupplierMovements = [],
            Payments = [],
            Forecast = [],
            Sales = new SalesSummary(0m, 0m, 0, 0m, 0m, 0, false, false, false, "ՀԾ վաճառքի տվյալները հասանելի չեն։"),
            Recommendations = [],
            Tasks = []
        });

    public Task<BusinessSummary> GetBusinessSummaryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BusinessSummary(startDate, endDate, new SalesSummary(0m, 0m, 0, 0m, 0m, 0, false, false, false, "ՀԾ վաճառքի տվյալները հասանելի չեն։"), [], [], 0m, 0m));

    public Task<BankSalesBreakdown> GetNonCashSalesAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        Task.FromResult(BankSalesBreakdown.Empty);

    public Task<IReadOnlyList<CashDocumentRecord>> GetCashDocumentsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CashDocumentRecord>>([]);

    public Task<IReadOnlyList<PurchaseProposal>> GetPurchaseProposalsAsync(DateOnly stockDate, DateOnly deliveryDate,
        IReadOnlyList<string> scheduledSuppliers, IReadOnlyDictionary<string, int> supplierCoverageDays,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseProposal>>([]);

    public Task<IReadOnlyList<SupplierSalesAnalysis>> GetSupplierSalesAnalysisAsync(DateOnly startDate, DateOnly endDate,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierSalesAnalysis>>([]);

    public Task<IReadOnlyList<SupplierActivityLine>> GetSupplierActivityAsync(string supplier, DateOnly startDate, DateOnly endDate,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierActivityLine>>([]);
}

public sealed class AppServices(IHtsDataProvider provider)
{
    public IHtsDataProvider DataProvider { get; private set; } = provider;
    public void UseDataProvider(IHtsDataProvider provider) => DataProvider = provider;
}
