using System.Globalization;

namespace PatarikAIOS;

/// <summary>Simple, auditable manager input format: աշխատավարձ / Աշխատող / 12000 / նշում.</summary>
public static class SalaryTelegramParser
{
    public static bool TryParse(string input, DateOnly defaultDate, out DateOnly date, out string employee, out decimal amount, out string note)
    {
        date = defaultDate; employee = string.Empty; amount = 0m; note = string.Empty;
        var parts = input.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;
        var first = parts[0];
        if (!first.StartsWith("աշխատավարձ", StringComparison.OrdinalIgnoreCase)) return false;
        var tail = first["աշխատավարձ".Length..].Trim();
        if (TryDate(tail, out var firstDate)) date = firstDate;

        var detailParts = parts.Skip(1).ToList();
        var dateIndex = detailParts.FindIndex(x => TryDate(x, out _));
        if (dateIndex >= 0 && TryDate(detailParts[dateIndex], out var enteredDate)) date = enteredDate;
        var amountIndex = detailParts.FindIndex(TryAmountOnly);
        if (amountIndex < 0) return false;
        amount = ParseAmount(detailParts[amountIndex]);
        var employeeParts = detailParts.Where((_, index) => index != dateIndex && index != amountIndex).ToList();
        if (!string.IsNullOrWhiteSpace(tail) && !TryDate(tail, out _)) employeeParts.Insert(0, tail);
        if (employeeParts.Count == 0) return false;
        employee = employeeParts[0].Trim();
        note = employeeParts.Count > 1 ? string.Join(" / ", employeeParts.Skip(1)) : string.Empty;
        return !string.IsNullOrWhiteSpace(employee) && amount > 0m;
    }

    private static bool TryDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value.Replace(',', '.'), ["dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private static bool TryAmountOnly(string value) => decimal.TryParse(value.Replace(" ", "").Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0m;
    private static decimal ParseAmount(string value) => decimal.Parse(value.Replace(" ", "").Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture);
}
