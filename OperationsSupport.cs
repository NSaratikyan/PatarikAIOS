using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

public static class PaymentReconciliation
{
    public static decimal Unrecorded(decimal confirmed, decimal imported, decimal manual) => Math.Max(0m, confirmed - imported - manual);
    public static decimal Completed(decimal plannedConfirmed, decimal recorded) => Math.Max(plannedConfirmed, recorded);
    public static decimal Remaining(decimal planned, decimal completed) => Math.Max(0m, planned - completed);
}

public static class AtomicJsonFile
{
    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}

public static class SupplierNameSuggestions
{
    public static string Key(string value) => new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static IReadOnlyList<string> Find(string input, IEnumerable<string> candidates)
    {
        var key = Key(input).Replace("կիսաֆաբրիկատ", "կֆ");
        if (key.Length == 0) return [];
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new { Name = name, Score = Score(key, Key(name).Replace("կիսաֆաբրիկատ", "կֆ")) })
            .Where(x => x.Score >= 0.55).OrderByDescending(x => x.Score).ThenBy(x => x.Name)
            .Take(3).Select(x => x.Name).ToList();
    }

    private static double Score(string a, string b)
    {
        if (a == b) return 1;
        if (Math.Min(a.Length, b.Length) >= 4 && (a.Contains(b) || b.Contains(a))) return 0.9;
        if (b.Length == 0) return 0;
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1]; current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            previous = current;
        }
        return 1d - (double)previous[b.Length] / Math.Max(a.Length, b.Length);
    }
}

public sealed class OperationsState
{
    public Dictionary<string, string> SupplierAliases { get; set; } = new();
    public Guid? PendingSupplierCorrection { get; set; }
    public HashSet<string> DeliveredSections { get; set; } = new();
}

public sealed class OperationsStateStore
{
    private readonly string _path;
    public OperationsStateStore(string? path = null) => _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "operations-state.json");
    public OperationsState Load() => File.Exists(_path)
        ? JsonSerializer.Deserialize<OperationsState>(File.ReadAllText(_path)) ?? new() : new();
    public void Save(OperationsState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, _path, true);
    }
}

public static class TelegramTextSections
{
    public static IEnumerable<string> Split(string text, int limit = 3500)
    {
        if (limit < 2) throw new ArgumentOutOfRangeException(nameof(limit));
        while (text.Length > limit)
        {
            var cut = text.LastIndexOf('\n', limit - 1, limit);
            if (cut < limit / 2) cut = limit;
            if (char.IsHighSurrogate(text[cut - 1])) cut--;
            yield return text[..cut];
            text = text[cut..].TrimStart('\n');
        }
        if (text.Length > 0) yield return text;
    }
}
