using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

/// <summary>Actual payments entered by the owner. These are intentionally separate from planned payments.</summary>
public sealed record CompletedPayment(string Recipient, decimal Amount, DateOnly PaidDate, string Note, string? SourceDocument = null, string? CashSource = null);

public sealed class LocalCompletedPaymentStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "data", "completed-payments.json");
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public List<CompletedPayment> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<CompletedPayment>>(File.ReadAllText(_path), _options) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    public void Save(IEnumerable<CompletedPayment> payments)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(payments, _options));
    }
}
