using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

/// <summary>Stores generated product-level preliminary orders by delivery day.</summary>
public sealed record PurchaseProposalBatch(DateOnly DeliveryDate, DateTime GeneratedAt, List<PurchaseProposal> Items);

public sealed class LocalPurchaseProposalStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "purchase-proposals.json");

    public IReadOnlyList<PurchaseProposal> Load(DateOnly deliveryDate)
    {
        try
        {
            if (!File.Exists(_path)) return [];
            return (JsonSerializer.Deserialize<List<PurchaseProposalBatch>>(File.ReadAllText(_path)) ?? [])
                .Where(x => x.DeliveryDate == deliveryDate).OrderByDescending(x => x.GeneratedAt).FirstOrDefault()?.Items ?? [];
        }
        catch (JsonException) { return []; }
    }

    public void Save(DateOnly deliveryDate, IEnumerable<PurchaseProposal> proposals)
    {
        List<PurchaseProposalBatch> batches;
        try { batches = File.Exists(_path) ? JsonSerializer.Deserialize<List<PurchaseProposalBatch>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { batches = []; }
        batches.RemoveAll(x => x.DeliveryDate == deliveryDate);
        batches.Add(new PurchaseProposalBatch(deliveryDate, DateTime.Now, proposals.ToList()));
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(batches, new JsonSerializerOptions { WriteIndented = true }));
    }
}
