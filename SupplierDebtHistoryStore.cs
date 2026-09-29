using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

/// <summary>Immutable audit trail for an owner's supplier-debt corrections and movement updates.</summary>
public sealed record SupplierDebtChange(
    Guid Id,
    DateOnly EffectiveDate,
    string Supplier,
    decimal PreviousDebt,
    decimal NewDebt,
    string Reason,
    DateTime ChangedAt,
    string ChangedBy);

public sealed class SupplierDebtHistoryStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "supplier-debt-history.json");

    public List<SupplierDebtChange> Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<List<SupplierDebtChange>>(File.ReadAllText(_path)) ?? []
                : [];
        }
        catch (JsonException) { return []; }
    }

    public void Save(IEnumerable<SupplierDebtChange> items)
    {
        AtomicJsonFile.Save(_path, items.ToList());
    }
}
