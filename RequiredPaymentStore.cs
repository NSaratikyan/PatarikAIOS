using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

/// <summary>Recurring fixed obligations entered by the owner (salary, rent, tax, loan, utilities, etc.).</summary>
public sealed record RequiredPaymentTemplate(Guid Id, string Category, string Name, decimal Amount, int PaymentDay,
    string Note, bool IsActive = true, bool RepeatsMonthly = true, DateOnly? OnlyForMonth = null);

public static class RequiredPaymentRules
{
    public static bool AppliesInMonth(RequiredPaymentTemplate payment, DateOnly date) => payment.IsActive &&
        (payment.RepeatsMonthly || (payment.OnlyForMonth?.Year == date.Year && payment.OnlyForMonth?.Month == date.Month));

    public static bool AppliesOn(RequiredPaymentTemplate payment, DateOnly date) =>
        AppliesInMonth(payment, date) && payment.PaymentDay == date.Day;

    public static string ScopeLabel(RequiredPaymentTemplate payment) => payment.RepeatsMonthly
        ? "Կրկնվում է ամեն ամիս"
        : $"Միայն {payment.OnlyForMonth:MM.yyyy}";
}

public sealed class RequiredPaymentStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "data", "required-payments.json");
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    /// <summary>Returns only owner-entered payments. No example payment is ever created.</summary>
    public List<RequiredPaymentTemplate> LoadOrSeed()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<List<RequiredPaymentTemplate>>(File.ReadAllText(_path), _options) ?? [];
        }
        catch (JsonException) { }
        return [];
    }

    public void Save(IEnumerable<RequiredPaymentTemplate> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, _options));
    }
}
