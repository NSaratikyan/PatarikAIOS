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
        var employeeIndex = 1;
        if (TryDate(tail, out var enteredDate))
        {
            date = enteredDate;
        }
        else if (!string.IsNullOrWhiteSpace(tail))
        {
            // Also allow: աշխատավարձ Լուսինե / 12000 / նշում
            employee = tail; employeeIndex = 1;
            if (parts.Length < 2 || !TryAmount(parts[1], out amount)) return false;
            note = parts.Length > 2 ? string.Join(" / ", parts.Skip(2)) : string.Empty;
            return true;
        }
        if (parts.Length <= employeeIndex + 1 || !TryAmount(parts[employeeIndex + 1], out amount)) return false;
        employee = parts[employeeIndex];
        note = parts.Length > employeeIndex + 2 ? string.Join(" / ", parts.Skip(employeeIndex + 2)) : string.Empty;
        return !string.IsNullOrWhiteSpace(employee);
    }

    private static bool TryDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, ["dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private static bool TryAmount(string value, out decimal amount) => decimal.TryParse(value.Replace(" ", "").Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
}
