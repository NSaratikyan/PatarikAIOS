using System.Text.Json;

namespace PatarikAIOS;

public static class PendingOrderEditing
{
    public static bool Unchanged(PendingEmployeeOrderChange expected, PendingEmployeeOrderChange current) =>
        JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(current);

    public static PendingEmployeeOrderChange Apply(PendingEmployeeOrderChange change, PendingOrderEditValues values,
        SupplierWeekPlanRow? baseline, DateTime at)
    {
        if (string.IsNullOrWhiteSpace(values.Supplier) || values.Order < 0 || values.Payment < 0 || values.OldDebtPayment < 0)
            throw new ArgumentException("Լրացրեք անունը և ոչ բացասական գումարներ։");
        var supplier = values.Supplier.Trim();
        var same = string.Equals(change.Supplier, supplier, StringComparison.OrdinalIgnoreCase);
        if (!same && baseline is not null && !string.Equals(baseline.Supplier, supplier, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Պլանը չի համապատասխանում ընտրված մատակարարին։");
        var history = (change.Corrections ?? []).ToList();
        history.Add(new PendingOrderCorrection(at, change.Supplier, supplier, change.ActualOrder, values.Order,
            change.ActualPayment, values.Payment, change.ActualOldDebtPayment, values.OldDebtPayment, "Տնօրեն · ծրագիր"));
        return change with
        {
            Supplier = supplier, OriginalSupplier = change.OriginalSupplier ?? change.Supplier,
            ActualOrder = values.Order, ActualPayment = values.Payment, ActualOldDebtPayment = values.OldDebtPayment,
            PlannedOrder = same ? change.PlannedOrder : baseline?.OrderAmount ?? 0m,
            PlannedPayment = same ? change.PlannedPayment : baseline?.PaymentAmount ?? 0m,
            PlannedOldDebtPayment = same ? change.PlannedOldDebtPayment : baseline?.OldDebtPayment ?? 0m,
            RequiresSupplierReview = false, SuggestedSuppliers = null,
            Revision = change.Revision + 1, Corrections = history
        };
    }
}
