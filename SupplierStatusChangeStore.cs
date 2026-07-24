using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

public sealed record SupplierStatusChange(DateOnly Date, string Supplier, string PreviousStatus, string NewStatus, string Note, DateTime ChangedAt, string ChangedBy);

public sealed class SupplierStatusChangeStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "supplier-status-history.json");
    public List<SupplierStatusChange> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<SupplierStatusChange>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<SupplierStatusChange> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}
