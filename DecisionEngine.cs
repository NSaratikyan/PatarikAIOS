namespace PatarikAIOS;

/// <summary>
/// Deterministic business-decision layer. Financial calculations stay in code;
/// a future language model may explain the result, but may not invent figures.
/// </summary>
public static class DecisionEngine
{
    public static DashboardSnapshot Evaluate(DashboardSnapshot source, IReadOnlyList<SupplierWeekPlanRow> supplierPlan, IEnumerable<RequiredPaymentTemplate> requiredPayments)
    {
        var today = source.Date;
        var mandatoryOther = source.Payments.Where(x => x.DueDate == today && x.IsMandatory).Sum(x => x.Amount)
            + requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, today)).Sum(x => x.Amount);
        var supplierOutflow = supplierPlan.Sum(x => x.PaymentAmount + x.OldDebtPayment);
        var plannedOutflow = supplierOutflow + mandatoryOther;
        var projectedBalance = source.Cash.Available - plannedOutflow;

        var plannedNames = supplierPlan.Select(x => Normalize(x.Supplier)).ToHashSet();
        var maximumDebt = Math.Max(1m, source.Suppliers.Select(x => Math.Max(0m, x.Debt)).DefaultIfEmpty(0m).Max());
        var scoredSuppliers = source.Suppliers.Select(supplier =>
        {
            var debtPart = (int)Math.Round(Math.Clamp(supplier.Debt / maximumDebt, 0m, 1m) * 55m);
            var deliveryPart = plannedNames.Contains(Normalize(supplier.Name)) ? 25 : supplier.NextSupplyDate <= today.AddDays(2) ? 15 : 5;
            var agePart = source.SupplierMovements.Any(x => Normalize(x.Supplier) == Normalize(supplier.Name) && x.OpeningDebt > 0m) ? 10 : 0;
            return supplier with { PriorityScore = Math.Clamp(debtPart + deliveryPart + agePart, 0, 100) };
        }).OrderByDescending(x => x.PriorityScore).ThenByDescending(x => x.Debt).ToList();

        var recommendations = new List<AiRecommendation>();
        if (plannedOutflow > 0m && projectedBalance < 0m)
            recommendations.Add(new("Դրամական բացի ռիսկ", Severity.Critical,
                $"Այսօրվա պլանավորված վճարումները՝ {plannedOutflow:N0} ֏, գերազանցում են հասանելի {source.Cash.Available:N0} ֏ միջոցները։",
                $"Չհաստատել բոլոր վճարումները միանգամից․ վերանայել առնվազն {Math.Abs(projectedBalance):N0} ֏ և առաջնահերթ պահել բարձր գնահատական ունեցող մատակարարներին։",
                $"Հաշվարկ՝ հասանելի {source.Cash.Available:N0} ֏ − վճարումներ {plannedOutflow:N0} ֏ = {projectedBalance:N0} ֏։"));
        else if (plannedOutflow > 0m && projectedBalance < source.Cash.Available * 0.10m)
            recommendations.Add(new("Ցածր ազատ մնացորդ", Severity.Warning,
                $"Վճարումներից հետո կմնա {projectedBalance:N0} ֏, որը հասանելի միջոցների 10%-ից ցածր է։",
                "Հետաձգել ոչ պարտադիր ծախսերը և պահպանել նվազագույն պահուստ։",
                $"Պլանային վճարումներ՝ {plannedOutflow:N0} ֏, ներառյալ մատակարարներ՝ {supplierOutflow:N0} ֏։"));

        var topSupplier = scoredSuppliers.FirstOrDefault(x => x.Debt > 0m && (plannedNames.Contains(Normalize(x.Name)) || x.NextSupplyDate <= today.AddDays(2)));
        if (topSupplier is not null)
            recommendations.Add(new($"Վճարման առաջնահերթություն՝ {topSupplier.Name}", topSupplier.PriorityScore >= 70 ? Severity.Critical : Severity.Warning,
                $"Պարտքը՝ {topSupplier.Debt:N0} ֏, հաշվարկված առաջնահերթությունը՝ {topSupplier.PriorityScore}/100։",
                "Վճարման վերջնական չափը հաստատելուց առաջ ստուգել մատակարարման պայմանավորվածությունը և պարտքի ժամկետը։",
                "Գնահատականը կազմվել է պարտքի չափից, մոտակա մատակարարումից և բաց պարտքի առկայությունից։"));

        if (source.Sales.PreviousSalesAmount > 0m && source.Sales.SalesChange <= -source.Sales.PreviousSalesAmount * 0.15m)
            recommendations.Add(new("Վաճառքի նկատելի անկում", Severity.Warning,
                $"Վաճառքը նվազել է {Math.Abs(source.Sales.SalesChange):N0} ֏ ({Math.Abs(source.Sales.SalesChange / source.Sales.PreviousSalesAmount * 100m):0.#}%) նախորդ օրվա համեմատ։",
                "Վերանայել վաճառքի ժամերը, մնացորդները և առաջիկա պատվերների ծավալը։",
                $"Այսօր՝ {source.Sales.SalesAmount:N0} ֏, նախորդ օր՝ {source.Sales.PreviousSalesAmount:N0} ֏։", false));

        if (recommendations.Count == 0)
            recommendations.Add(new("Օրվա հիմնական ցուցանիշները կառավարելի են", Severity.Info,
                "Հաշվարկված պարտադիր վճարումների հիման վրա դրամական բաց չի հայտնաբերվել։",
                "Շարունակել փաստացի մատակարարումների և վճարումների գրանցումը։",
                $"Հասանելի միջոցներ՝ {source.Cash.Available:N0} ֏, պլանային վճարումներ՝ {plannedOutflow:N0} ֏։", false));

        return new DashboardSnapshot
        {
            Date = source.Date, Cash = source.Cash, Suppliers = scoredSuppliers,
            SupplierMovements = source.SupplierMovements, Payments = source.Payments,
            Forecast = source.Forecast, Sales = source.Sales, Recommendations = recommendations, Tasks = source.Tasks
        };
    }

    private static string Normalize(string value) => new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
