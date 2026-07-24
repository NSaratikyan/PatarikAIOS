namespace PatarikAIOS;

public enum Severity { Info, Warning, Critical }

public sealed record CashPosition(decimal Cash, decimal Bank)
{
    public decimal Available => Cash + Bank;
}

/// <summary>Owner-configured breakdown of the available-money card.</summary>
public sealed record AvailableFundsSettings(
    decimal? CashVault = null,
    decimal? CashDesk = null,
    decimal? BankReport = null,
    decimal? AmeriabankPos099 = null,
    decimal? Idram = null,
    DateOnly? OpeningMonth = null);

public sealed record AvailableFundsBreakdown(
    decimal CashVault,
    decimal CashDesk,
    decimal BankReport,
    decimal AmeriabankPos099,
    decimal Idram)
{
    public decimal Cash => CashVault + CashDesk;
    public decimal Bank => BankReport + AmeriabankPos099 + Idram;
    public decimal Total => Cash + Bank;
}

public sealed record BankSalesBreakdown(decimal BankReport, decimal AmeriabankPos099, decimal Idram)
{
    public static readonly BankSalesBreakdown Empty = new(0m, 0m, 0m);
}

public sealed record CashDeskAdjustment(DateOnly Date, string CashDesk, decimal ClosingBalance, string Note);

public sealed record CashLedgerMovement(
    DateOnly Date,
    string SourceCashDesk,
    string? TargetCashDesk,
    decimal Amount,
    string DocumentNumber,
    string Partner,
    string ContractOrReason,
    bool IsInternalTransfer);

public sealed record Supplier(string Name, string Direction, decimal Debt, DateOnly NextSupplyDate,
    int PriorityScore, string PaymentMode, string Note);

/// <summary>
/// One supplier's movement for one calendar day. PaymentForOrder is money paid for today's receipt;
/// OldDebtPayment is separately shown so the owner can distinguish it from a new order payment.
/// </summary>
public sealed record SupplierDailyMovement(
    DateOnly Date,
    string Supplier,
    string Direction,
    decimal OpeningDebt,
    decimal OrderAmount,
    decimal PaymentForOrder,
    decimal OldDebtPayment,
    string? OldDebtDueDate,
    string Note,
    bool IsPlanned = false)
{
    public decimal OrderUnpaid => OrderAmount - PaymentForOrder;
    public decimal DebtChange => OrderUnpaid - OldDebtPayment;
    public decimal ClosingDebt => OpeningDebt + DebtChange;
}

public sealed record PlannedPayment(string Supplier, decimal Amount, DateOnly DueDate,
    bool IsMandatory, string Reason);

public sealed record CashForecast(DateOnly Date, decimal ExpectedIncome, decimal ExpectedOutflow,
    decimal ClosingBalance);

public sealed record SalesSummary(
    decimal SalesAmount,
    decimal CostAmount,
    int ReceiptCount,
    decimal PreviousSalesAmount,
    decimal PreviousProfitAmount,
    int PreviousReceiptCount)
{
    public decimal Profit => SalesAmount - CostAmount;
    public decimal AverageReceipt => ReceiptCount == 0 ? 0m : SalesAmount / ReceiptCount;
    public decimal SalesChange => SalesAmount - PreviousSalesAmount;
    public decimal ProfitChange => Profit - PreviousProfitAmount;
    public int ReceiptChange => ReceiptCount - PreviousReceiptCount;
}

public sealed record EmployeeShiftSale(string Employee, string Shift, decimal SalesAmount, int ReceiptCount);

public sealed record SupplyByStorage(string Storage, decimal Amount, bool IsProduction);

public sealed record BusinessSummary(
    DateOnly StartDate,
    DateOnly EndDate,
    SalesSummary Sales,
    IReadOnlyList<EmployeeShiftSale> EmployeeSales,
    IReadOnlyList<SupplyByStorage> Supplies,
    decimal SupplierPayments,
    decimal DebtChange)
{
    public decimal SuppliedAmount => Supplies.Sum(x => x.Amount);
    public decimal ProductionSuppliedAmount => Supplies.Where(x => x.IsProduction).Sum(x => x.Amount);
}

public sealed record AiRecommendation(string Title, Severity Severity, string Finding,
    string SuggestedAction, string Evidence, bool RequiresApproval = true);

public sealed record DailyTask(string Owner, string Task, string Deadline, string Status);

public sealed record SupplierDeliveryPattern(
    DayOfWeek Weekday,
    string Supplier,
    int DeliveryCount,
    decimal AverageOrderAmount,
    decimal SuggestedOrderAmount,
    string? OwnerInstruction = null,
    bool IsOneTime = false);

/// <summary>One product line proposed for a supplier delivery.</summary>
public sealed record PurchaseProposal(
    DateOnly DeliveryDate,
    string Supplier,
    string Product,
    string Storage,
    string Unit,
    decimal CurrentQuantity,
    decimal MinimumQuantity,
    decimal MaximumQuantity,
    decimal ProposedQuantity,
    decimal UnitCost,
    string Reason)
{
    public decimal Amount => ProposedQuantity * UnitCost;
}

/// <summary>Sales grouped by the supplier linked to the sold product batch in HTS.</summary>
public sealed record SupplierSalesAnalysis(
    string Supplier,
    decimal SalesAmount,
    decimal CostAmount,
    decimal Quantity,
    int ProductCount,
    int StorageCount)
{
    public decimal Profit => SalesAmount - CostAmount;
}

public sealed class DashboardSnapshot
{
    public required DateOnly Date { get; init; }
    public required CashPosition Cash { get; init; }
    public required IReadOnlyList<Supplier> Suppliers { get; init; }
    public required IReadOnlyList<SupplierDailyMovement> SupplierMovements { get; init; }
    public required IReadOnlyList<PlannedPayment> Payments { get; init; }
    public required IReadOnlyList<CashForecast> Forecast { get; init; }
    public required SalesSummary Sales { get; init; }
    public required IReadOnlyList<AiRecommendation> Recommendations { get; init; }
    public required IReadOnlyList<DailyTask> Tasks { get; init; }
}
