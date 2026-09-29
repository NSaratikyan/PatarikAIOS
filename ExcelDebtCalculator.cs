namespace PatarikAIOS;

public sealed record ExcelDebtResult(decimal? Debt, string Warning);
public static class ExcelDebtCalculator
{
    public static ExcelDebtResult Calculate(ExcelImportState state, string supplier, DateOnly date, DateOnly? balanceDate, decimal opening)
    {
        if (balanceDate is null) return new(null,"Սկզբնական պարտքի ամսաթիվն ու գումարը պետք է հաստատել։");
        if (date < balanceDate) return new(null,"Սկզբնական պարտքի ամսաթվից առաջ հաշվարկ չկա։");
        var required = Enumerable.Range(1,date.DayNumber-balanceDate.Value.DayNumber).Select(i=>balanceDate.Value.AddDays(i)).ToList();
        foreach (var kind in new[]{"cash","warehouse"})
        {
            var covered = state.Batches.Where(x=>x.Kind==kind).SelectMany(x=>x.Dates).ToHashSet();
            if (required.Any(d=>!covered.Contains(d))) return new(null,"Պարտքի ամբողջական հաշվարկի համար պակասում են օրերի դրամական կամ ստացումների ֆայլերը։");
        }
        var receipts = state.Batches.SelectMany(x=>x.Receipts).Where(x=>x.Supplier==supplier && x.Date>balanceDate && x.Date<=date).Sum(x=>x.Amount);
        var payments = CashDocumentImportService.SupplierPaymentRows(state.Batches.SelectMany(x=>x.Cash))
            .Where(x=>x.Recipient==supplier && x.Date>balanceDate && x.Date<=date).Sum(x=>x.Amount);
        return new(opening+receipts-payments,"Excel ստացումներ և կանխիկ վճարումներ․ վերադարձները և բանկային վճարումները պետք է համադրել առանձին։");
    }
}
