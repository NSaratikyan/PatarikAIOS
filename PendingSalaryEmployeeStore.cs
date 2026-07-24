using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

/// <summary>A manager-proposed salary entry for a previously unknown employee.</summary>
public sealed record PendingSalaryEmployee(Guid Id, SalaryAccrual ProposedAccrual, DateTime RequestedAt);

public sealed class PendingSalaryEmployeeStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "data", "pending-salary-employees.json");
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    public List<PendingSalaryEmployee> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<PendingSalaryEmployee>>(File.ReadAllText(_path), _options) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<PendingSalaryEmployee> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(rows, _options));
    }
}
