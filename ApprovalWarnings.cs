namespace PatarikAIOS;

public static class ApprovalWarnings
{
    public static bool Unknown(PendingEmployeeOrderChange row, IReadOnlyList<string>? known) =>
        row.RequiresSupplierReview || known is { Count: > 0 } && !known.Any(x => SupplierNameSuggestions.Key(x) == SupplierNameSuggestions.Key(row.Supplier));
    public static bool Duplicate(PendingEmployeeOrderChange row, IEnumerable<PendingEmployeeOrderChange> all) =>
        all.Any(x => x.Id != row.Id && x.Date == row.Date &&
            SupplierNameSuggestions.Key(x.Supplier) == SupplierNameSuggestions.Key(row.Supplier) &&
            x.ActualOrder == row.ActualOrder && x.ActualPayment == row.ActualPayment && x.ActualOldDebtPayment == row.ActualOldDebtPayment);
}
