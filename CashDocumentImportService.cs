using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace PatarikAIOS;

public sealed record CashDocumentRecord(DateOnly Date, string DocumentNumber, string Type, decimal Amount,
    string Information, string Recipient, string CashBox, string Shift, string Employee);

public sealed record CashDailySummary(DateOnly Date, decimal Sales, int ReceiptCount, decimal SupplierPayments,
    decimal OtherExpenses, decimal CashClosings, int DocumentCount)
{
    public decimal DebtChange => -SupplierPayments;
}

/// <summary>Imports the daily «Դրամական փաստաթղթեր» Excel export from ՀԾ.</summary>
public static class CashDocumentImportService
{
    public static IReadOnlyList<CashDocumentRecord> Import(string workbookPath)
    {
        using var archive = ZipFile.OpenRead(workbookPath);
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        var shared = ReadSharedStrings(archive, ns);
        var sheet = LoadXml(archive, "xl/worksheets/sheet1.xml");
        var rows = sheet.Descendants(ns + "row").Select(row => ReadRow(row, ns, shared)).ToList();

        var headerRow = rows.FindIndex(row => row.Any(x => Normalize(x) == "ամսաթիվ") && row.Any(x => Normalize(x) == "գումար"));
        if (headerRow < 0) throw new InvalidOperationException("Չգտնվեց «Ամսաթիվ» և «Գումար» սյունակներով դրամարկղային հաշվետվություն։");
        var headers = rows[headerRow].Select((value, index) => (Key: Normalize(value), Index: index))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key)).ToDictionary(x => x.Key, x => x.Index);

        var dateColumn = Column(headers, "ամսաթիվ");
        var typeColumn = Column(headers, "տեսակ");
        var amountColumn = Column(headers, "գումար");
        var documentColumn = Column(headers, "փաստաթղթին");
        var informationColumn = Column(headers, "տեղեկություն");
        var partnerColumn = Column(headers, "գործընկերոջանվանում", optional: true);
        var cashBoxColumn = Column(headers, "դրամարկղ", optional: true);
        var shiftColumn = Column(headers, "հերթափոխ", optional: true);
        var employeeColumn = Column(headers, "աշխատակից", optional: true);

        var records = new List<CashDocumentRecord>();
        foreach (var row in rows.Skip(headerRow + 1))
        {
            var date = ReadDate(Cell(row, dateColumn));
            var amount = ReadAmount(Cell(row, amountColumn));
            var type = Cell(row, typeColumn).Trim();
            if (!date.HasValue || !amount.HasValue || string.IsNullOrWhiteSpace(type)) continue;
            var information = Cell(row, informationColumn).Trim();
            var partner = partnerColumn >= 0 ? Cell(row, partnerColumn).Trim() : string.Empty;
            records.Add(new CashDocumentRecord(date.Value, Cell(row, documentColumn).Trim(), type, amount.Value,
                information, string.IsNullOrWhiteSpace(partner) ? information : partner,
                cashBoxColumn >= 0 ? Cell(row, cashBoxColumn).Trim() : string.Empty,
                shiftColumn >= 0 ? Cell(row, shiftColumn).Trim() : string.Empty,
                employeeColumn >= 0 ? Cell(row, employeeColumn).Trim() : string.Empty));
        }
        if (records.Count == 0) throw new InvalidOperationException("Ֆայլում օգտագործելի դրամարկղային գրանցումներ չգտնվեցին։");
        return records;
    }

    public static CashDailySummary Summary(IEnumerable<CashDocumentRecord> source, DateOnly date)
    {
        var rows = source.Where(x => x.Date == date).ToList();
        var sales = rows.Where(IsSale).ToList();
        var exits = rows.Where(IsExit).ToList();
        var closings = exits.Where(IsClosing).ToList();
        var other = exits.Where(x => !IsClosing(x) && IsOtherExpense(x)).ToList();
        var supplier = exits.Where(x => !IsClosing(x) && !IsOtherExpense(x)).ToList();
        return new CashDailySummary(date, sales.Sum(x => x.Amount), sales.Count, supplier.Sum(x => x.Amount),
            other.Sum(x => x.Amount), closings.Sum(x => x.Amount), rows.Count);
    }

    public static IEnumerable<CashDocumentRecord> SupplierPaymentRows(IEnumerable<CashDocumentRecord> source) =>
        source.Where(x => (IsExit(x) || HasCanonicalType(x, "cashoutput")) && !IsCashTransfer(x) && !IsOtherExpense(x));

    /// <summary>
    /// Converts HԾ cash documents to movements of cash desks. Sales are received to 0001.
    /// A closing or "output to another cash desk" is an internal transfer, never an expense.
    /// </summary>
    public static IEnumerable<CashLedgerMovement> CashDeskMovements(IEnumerable<CashDocumentRecord> source)
    {
        var all = source.ToList();
        var verifiedSalesDays = all.Where(x => x.Type == "ecr-cash-sales").Select(x => x.Date).ToHashSet();
        foreach (var row in all)
        {
            var cashDesk = CashDeskCode(row.CashBox, "0001");
            if (row.Type == "ecr-cash-sales")
            {
                yield return new CashLedgerMovement(row.Date, row.Amount < 0 ? "0001" : "", row.Amount < 0 ? null : "0001",
                    Math.Abs(row.Amount), row.DocumentNumber, row.Recipient, row.Information, false);
                continue;
            }
            if (IsSale(row) || HasCanonicalType(row, "sale"))
            {
                if (verifiedSalesDays.Contains(row.Date)) continue;
                yield return new CashLedgerMovement(row.Date, string.Empty, "0001", row.Amount, row.DocumentNumber, row.Recipient, row.Information, false);
                continue;
            }
            if (IsInput(row) || HasCanonicalType(row, "cashinput"))
            {
                yield return new CashLedgerMovement(row.Date, string.Empty, cashDesk, row.Amount, row.DocumentNumber, row.Recipient, row.Information, false);
                continue;
            }
            if (!IsExit(row) && !HasCanonicalType(row, "cashoutput") && !HasCanonicalType(row, "cashtransfer")) continue;
            var transfer = IsCashTransfer(row);
            var target = transfer ? TargetCashDesk(row, cashDesk) : null;
            yield return new CashLedgerMovement(row.Date, cashDesk, target, row.Amount, row.DocumentNumber, row.Recipient, row.Information, transfer);
        }
    }

    private static bool IsSale(CashDocumentRecord row) => Normalize(row.Type).Contains("վաճառք");
    private static bool IsInput(CashDocumentRecord row) => Normalize(row.Type).Contains("մուտքի");
    private static bool IsExit(CashDocumentRecord row) => Normalize(row.Type).Contains("ելքի");
    private static bool IsClosing(CashDocumentRecord row) => Normalize(row.Information).Contains("մնացորդիփակում");
    private static bool IsOtherExpense(CashDocumentRecord row) => Normalize(row.Information).Contains("այլծախս");
    private static bool IsCashTransfer(CashDocumentRecord row) =>
        IsClosing(row) || HasCanonicalType(row, "cashtransfer") ||
        Normalize(row.Type).Contains("գումարիելքդեպիայլդրամարկղ");
    private static string TargetCashDesk(CashDocumentRecord row, string source)
    {
        var match = Regex.Match(row.Information ?? string.Empty, @"\b\d{4}\b");
        if (match.Success && match.Value != source) return match.Value;
        return source == "0001" ? "0002" : "0001";
    }
    private static string CashDeskCode(string value, string fallback)
    {
        var match = Regex.Match(value ?? string.Empty, @"\b\d{1,4}\b");
        return match.Success ? match.Value.PadLeft(4, '0') : fallback;
    }
    private static bool HasCanonicalType(CashDocumentRecord row, string type) =>
        (row.Type ?? string.Empty).Contains(type, StringComparison.OrdinalIgnoreCase);
    private static int Column(IReadOnlyDictionary<string, int> headers, string startsWith, bool optional = false)
    {
        var value = headers.FirstOrDefault(x => x.Key.StartsWith(startsWith, StringComparison.Ordinal)).Value;
        if (headers.Keys.Any(x => x.StartsWith(startsWith, StringComparison.Ordinal))) return value;
        if (optional) return -1;
        throw new InvalidOperationException($"Չգտնվեց «{startsWith}» սյունակը։");
    }
    private static string Normalize(string value) => new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    internal static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidOperationException($"Excel ֆայլում չի գտնվել {path}");
        using var stream = entry.Open(); return XDocument.Load(stream);
    }
    internal static List<string> ReadSharedStrings(ZipArchive archive, XNamespace ns)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml"); if (entry is null) return [];
        using var stream = entry.Open();
        return XDocument.Load(stream).Descendants(ns + "si").Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value))).ToList();
    }
    internal static List<string> ReadRow(XElement row, XNamespace ns, IReadOnlyList<string> shared)
    {
        var result = new List<string>();
        foreach (var cell in row.Elements(ns + "c"))
        {
            var index = ColumnIndex((string?)cell.Attribute("r") ?? "A1"); while (result.Count <= index) result.Add(string.Empty);
            var value = cell.Element(ns + "v")?.Value ?? cell.Descendants(ns + "t").FirstOrDefault()?.Value ?? string.Empty;
            result[index] = (string?)cell.Attribute("t") == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex < shared.Count ? shared[sharedIndex] : value;
        }
        return result;
    }
    private static int ColumnIndex(string reference)
    {
        var index = 0; foreach (var c in reference.TakeWhile(char.IsLetter)) index = index * 26 + char.ToUpperInvariant(c) - 'A' + 1; return index - 1;
    }
    private static string Cell(IReadOnlyList<string> row, int column) => column >= 0 && column < row.Count ? row[column] : string.Empty;
    internal static DateOnly? ReadDate(string value)
    {
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial) && serial > 20000) return DateOnly.FromDateTime(DateTime.FromOADate(serial));
        return DateTime.TryParse(value, out var date) ? DateOnly.FromDateTime(date) : null;
    }
    internal static decimal? ReadAmount(string value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || decimal.TryParse(value, out amount) ? amount : null;
}

public sealed class LocalCashDocumentStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "data", "cash-documents.json");
    public List<CashDocumentRecord> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<CashDocumentRecord>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void ReplaceDays(List<CashDocumentRecord> imported, List<CashDocumentRecord> all)
    {
        var days = imported.Select(x => x.Date).Distinct().ToHashSet();
        all.RemoveAll(x => days.Contains(x.Date)); all.AddRange(imported);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Upsert(List<CashDocumentRecord> imported, List<CashDocumentRecord> all)
    {
        foreach (var row in imported)
        {
            var index = all.FindIndex(x => x.Date == row.Date && x.DocumentNumber == row.DocumentNumber &&
                x.Type.Split('|')[0].Trim().Equals(row.Type.Split('|')[0].Trim(), StringComparison.OrdinalIgnoreCase) &&
                x.CashBox.Trim().PadLeft(4, '0') == row.CashBox.Trim().PadLeft(4, '0'));
            if (index < 0) all.Add(row);
            else all[index] = row;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }
}
