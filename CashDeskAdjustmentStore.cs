using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

public sealed class CashDeskAdjustmentStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "data", "cash-desk-adjustments.json");

    public List<CashDeskAdjustment> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<CashDeskAdjustment>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    public void Save(List<CashDeskAdjustment> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}
