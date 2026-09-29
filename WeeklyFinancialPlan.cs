namespace PatarikAIOS;

/// <summary>
/// A transparent weekly payment budget.  It deliberately separates money
/// actually due from the daily reserve that prepares for a later fixed cost.
/// </summary>
public sealed record WeeklyFinancialDay(
    DateOnly Date,
    decimal SalesPlan,
    decimal FixedCostReserve,
    decimal SalaryAccrual,
    decimal PlannedPayments,
    decimal DailyPaymentLimit,
    decimal DifferenceFromLimit,
    decimal NextDaysDailyLimit);

public sealed record WeeklyFinancialPlan(
    CashFlowAnalysis CashFlow,
    decimal BaselineWeekSales,
    decimal SalesVariance,
    decimal FixedPaymentsDue,
    decimal SalaryPaymentsDue,
    decimal SupplierPaymentsDue,
    decimal MinimumReserve,
    decimal FreeMoney,
    IReadOnlyList<WeeklyFinancialDay> Days)
{
    public bool IsWithinPlan => FreeMoney >= 0m;
}

public static class WeeklyFinancialPlanBuilder
{
    public static WeeklyFinancialPlan Build(
        CashFlowAnalysis cashFlow,
        decimal baselineWeekSales,
        decimal fixedPaymentsDue,
        decimal salaryPaymentsDue,
        decimal supplierPaymentsDue,
        Func<DateOnly, decimal> fixedReserve,
        Func<DateOnly, decimal> salaryAccrual)
    {
        var forecastWeekSales = cashFlow.WeekExpectedSales;
        var salesVariance = forecastWeekSales - baselineWeekSales;
        // The reserve is intentionally set aside before deciding how much can
        // be used for supplier payments or unplanned expenses.
        var paymentBudget = forecastWeekSales - cashFlow.MinimumReserve;
        var freeMoney = paymentBudget - fixedPaymentsDue - salaryPaymentsDue - supplierPaymentsDue;
        var remainingBudget = paymentBudget;
        var days = new List<WeeklyFinancialDay>();
        for (var index = 0; index < cashFlow.Days.Count; index++)
        {
            var day = cashFlow.Days[index];
            var remainingDays = cashFlow.Days.Count - index;
            var limit = remainingDays == 0 ? 0m : Math.Max(0m, remainingBudget / remainingDays);
            var difference = day.PlannedPayments - limit;
            remainingBudget -= day.PlannedPayments;
            var nextDays = remainingDays - 1;
            var nextLimit = nextDays == 0 ? 0m : Math.Max(0m, remainingBudget / nextDays);
            days.Add(new WeeklyFinancialDay(day.Date, day.ExpectedSales, fixedReserve(day.Date), salaryAccrual(day.Date),
                day.PlannedPayments, limit, difference, nextLimit));
        }

        return new WeeklyFinancialPlan(cashFlow, baselineWeekSales, salesVariance, fixedPaymentsDue,
            salaryPaymentsDue, supplierPaymentsDue, cashFlow.MinimumReserve, freeMoney, days);
    }
}
