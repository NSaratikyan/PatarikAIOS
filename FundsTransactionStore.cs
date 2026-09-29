using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

/// <summary>Persists owner-entered bank and cash payments outside of HTS imports.</summary>
public sealed class FundsTransactionStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "funds-transactions.json");

    public List<FundsTransaction> Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<List<FundsTransaction>>(File.ReadAllText(_path)) ?? []
                : [];
        }
        catch (JsonException) { return []; }
    }

    public void Save(IEnumerable<FundsTransaction> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}
