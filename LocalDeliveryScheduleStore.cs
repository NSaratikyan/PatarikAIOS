using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

public sealed class LocalDeliveryScheduleStore
{
    private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, "data", "supplier-delivery-schedule.json");
    public IReadOnlyList<SupplierDeliveryPattern> Load()
    {
        try { return File.Exists(_filePath) ? JsonSerializer.Deserialize<List<SupplierDeliveryPattern>>(File.ReadAllText(_filePath)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<SupplierDeliveryPattern> patterns)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(patterns, new JsonSerializerOptions { WriteIndented = true }));
    }
}
