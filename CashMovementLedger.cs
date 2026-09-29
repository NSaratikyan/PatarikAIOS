namespace PatarikAIOS;

public sealed record CashDayLedger(
    DateOnly Date,
    decimal CashDeskIn,
    decimal CashDeskOut,
    decimal CashDeskClosing,
    decimal VaultIn,
    decimal VaultOut,
    decimal VaultClosing)
{
    public decimal TotalCash => CashDeskClosing + VaultClosing;
    public decimal GrossSales { get; init; }
    public decimal NonCash { get; init; }
    public decimal OtherCashIn { get; init; }
    public bool IsManual { get; init; }
}

public static class CashBalanceCalculator
{
    public static decimal Balance(string cashDesk, decimal openingBalance, DateOnly start, DateOnly end,
        IEnumerable<CashDeskAdjustment> adjustments, IEnumerable<CashLedgerMovement> movements)
    {
        var correction = adjustments.Reverse().Where(x => x.CashDesk == cashDesk && x.Date >= start && x.Date <= end)
            .OrderByDescending(x => x.Date).FirstOrDefault();
        var balance = correction?.ClosingBalance ?? openingBalance;
        var movementStart = correction is null ? start : correction.Date.AddDays(1);
        foreach (var movement in movements.Where(x => x.Date >= movementStart && x.Date <= end))
        {
            if (movement.SourceCashDesk == cashDesk) balance -= movement.Amount;
            if (movement.TargetCashDesk == cashDesk) balance += movement.Amount;
        }
        return balance;
    }
}
