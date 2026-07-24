using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace PatarikAIOS;

/// <summary>Reads the simple HԾ Excel export without a third-party package.</summary>
public static class WarehouseImportService
{
    public static IReadOnlyList<SupplierDeliveryPattern> Import(string workbookPath)
    {
        using var archive = ZipFile.OpenRead(workbookPath);
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        var shared = ReadSharedStrings(archive, ns);
        var sheet = LoadXml(archive, "xl/worksheets/sheet1.xml");
        var rows = sheet.Descendants(ns + "row").Select(row => ReadRow(row, ns, shared)).ToList();
        if (rows.Count < 2) throw new InvalidOperationException("Ֆայլում ստացումների տողեր չեն գտնվել։");
        var header = rows[0].Select((value, index) => (value, index)).ToDictionary(x => x.value.Trim(), x => x.index);
        if (!header.TryGetValue("Ամսաթիվ", out var dateColumn) || !header.TryGetValue("Գումար", out var amountColumn) || !header.TryGetValue("Գործընկերոջ անվանում", out var supplierColumn))
            throw new InvalidOperationException("Ֆայլում պետք են «Ամսաթիվ», «Գումար», «Գործընկերոջ անվանում» սյունակները։");

        var records = rows.Skip(1).Select(row => new
        {
            Date = ReadDate(Cell(row, dateColumn)),
            Amount = ReadAmount(Cell(row, amountColumn)),
            Supplier = Cell(row, supplierColumn).Trim()
        }).Where(x => x.Date.HasValue && x.Amount.HasValue && !string.IsNullOrWhiteSpace(x.Supplier)).ToList();
        if (records.Count == 0) throw new InvalidOperationException("Ընթերցվող ստացումներ չեն գտնվել։");

        return records.GroupBy(x => new { Day = x.Date!.Value.DayOfWeek, x.Supplier })
            .OrderBy(x => DayOrder(x.Key.Day)).ThenBy(x => x.Key.Supplier)
            .Select(x =>
            {
                var average = decimal.Round(x.Average(y => y.Amount!.Value), 0, MidpointRounding.AwayFromZero);
                return new SupplierDeliveryPattern(x.Key.Day, x.Key.Supplier, x.Count(), average, average);
            }).ToList();
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidOperationException($"Excel կառուցվածքում չի գտնվել {path}");
        using var stream = entry.Open(); return XDocument.Load(stream);
    }
    private static List<string> ReadSharedStrings(ZipArchive archive, XNamespace ns)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        using var stream = entry.Open();
        return XDocument.Load(stream).Descendants(ns + "si").Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value))).ToList();
    }
    private static List<string> ReadRow(XElement row, XNamespace ns, IReadOnlyList<string> shared)
    {
        var result = new List<string>();
        foreach (var cell in row.Elements(ns + "c"))
        {
            var reference = (string?)cell.Attribute("r") ?? "A1";
            var index = ColumnIndex(reference);
            while (result.Count <= index) result.Add(string.Empty);
            var value = cell.Element(ns + "v")?.Value ?? cell.Descendants(ns + "t").FirstOrDefault()?.Value ?? string.Empty;
            result[index] = (string?)cell.Attribute("t") == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex < shared.Count ? shared[sharedIndex] : value;
        }
        return result;
    }
    private static int ColumnIndex(string cellReference)
    {
        var index = 0;
        foreach (var character in cellReference.TakeWhile(char.IsLetter)) index = index * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        return index - 1;
    }
    private static string Cell(IReadOnlyList<string> row, int column) => column < row.Count ? row[column] : string.Empty;
    private static DateTime? ReadDate(string value)
    {
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial) && serial > 20000) return DateTime.FromOADate(serial);
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || DateTime.TryParse(value, out date) ? date : null;
    }
    private static decimal? ReadAmount(string value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || decimal.TryParse(value, out amount) ? amount : null;
    private static int DayOrder(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;
}
