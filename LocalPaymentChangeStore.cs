using System.Text.Json;
using System.IO;

namespace PatarikAIOS;

/// <summary>
/// Local MVP persistence for owner-entered payment agreements.
/// The file stays next to the application, so it is easy to back up and later migrate to PostgreSQL.
/// </summary>
public sealed class LocalPaymentChangeStore
{
    private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, "data", "manual-payment-changes.json");
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public IReadOnlyList<PaymentChangeDraft> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return [];
            return JsonSerializer.Deserialize<List<PaymentChangeDraft>>(File.ReadAllText(_filePath), _options) ?? [];
        }
        catch (JsonException)
        {
            // Keep the application usable if a manually edited local file is malformed.
            return [];
        }
    }

    public void Save(IEnumerable<PaymentChangeDraft> changes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(changes, _options));
    }
}
