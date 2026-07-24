namespace PatarikAIOS;

/// <summary>
/// Transparent 7-day cash-flow calculation. Orders are shown separately from
/// cash payments: an unpaid order increases debt but is not treated as cash outflow.
/// </summary>
public sealed record CashFlowDayPlan(
    DateOnly Date,
    decimal ExpectedSales,
    decimal PlannedOrders,
    decimal SupplierPayments,
    decimal MandatoryPayments,
    decimal ClosingBalance)
{
    public decimal PlannedPayments => SupplierPayments + MandatoryPayments;
}

public sealed record CashFlowAnalysis(
    decimal OpeningBalance,
    decimal HistoricalDailySales,
    bool HasSalesHistory,
    decimal MinimumReserve,
    IReadOnlyList<CashFlowDayPlan> Days)
{
    public decimal WeekExpectedSales => Days.Sum(x => x.ExpectedSales);
    public decimal WeekSupplierPayments => Days.Sum(x => x.SupplierPayments);
    public decimal WeekMandatoryPayments => Days.Sum(x => x.MandatoryPayments);
    public decimal WeekOrders => Days.Sum(x => x.PlannedOrders);
    public decimal WeekClosingBalance => Days.LastOrDefault()?.ClosingBalance ?? OpeningBalance;
    /// <summary>Days where the balance is below the owner's safety reserve.</summary>
    public IReadOnlyList<CashFlowDayPlan> DeficitDays => Days.Where(x => x.ClosingBalance < MinimumReserve).ToList();
}

public static class CashFlowPlanner
{
    public static CashFlowAnalysis Build(
        DateOnly startDate,
        decimal openingBalance,
        decimal historicalDailySales,
        bool hasSalesHistory,
        decimal? actualSalesForStartDate,
        decimal minimumReserve,
        Func<DateOnly, IReadOnlyList<SupplierWeekPlanRow>> supplierPlan,
        Func<DateOnly, decimal> mandatoryPayments)
    {
        var balance = openingBalance;
        var days = new List<CashFlowDayPlan>();
        for (var offset = 0; offset < 7; offset++)
        {
            var date = startDate.AddDays(offset);
            var suppliers = supplierPlan(date);
            var orders = suppliers.Sum(x => x.OrderAmount);
            var supplierPayments = suppliers.Sum(x => x.PaymentAmount + x.OldDebtPayment);
            var mandatory = mandatoryPayments(date);
            var expectedSales = offset == 0 && actualSalesForStartDate.HasValue
                ? actualSalesForStartDate.Value
                : hasSalesHistory ? historicalDailySales : 0m;
            balance += expectedSales - supplierPayments - mandatory;
            days.Add(new CashFlowDayPlan(date, expectedSales, orders, supplierPayments, mandatory, balance));
        }
        return new CashFlowAnalysis(openingBalance, historicalDailySales, hasSalesHistory, minimumReserve, days);
    }
}
