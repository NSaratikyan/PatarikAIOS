using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

public sealed class AvailableFundsStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "available-funds.json");

    public AvailableFundsSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AvailableFundsSettings>(File.ReadAllText(_path)) ?? new AvailableFundsSettings();
        }
        catch (JsonException) { }
        return new AvailableFundsSettings();
    }

    public void Save(AvailableFundsSettings value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}
