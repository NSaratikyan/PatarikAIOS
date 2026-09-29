using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace PatarikAIOS;

public sealed record ExcelSale(DateOnly Date, string Document, string Receipt, string ProductCode, string Product,
    string Unit, string Supplier, decimal Quantity, decimal Sales, decimal Cost, string Storage, string Employee, string Shift);
public sealed record ExcelReceipt(DateOnly Date, string Document, string Supplier, string PartnerCode, string Storage, decimal Amount);
public sealed record ExcelBatch(string Kind, string FileName, DateTime ImportedAt, List<DateOnly> Dates,
    List<ExcelSale> Sales, List<ExcelReceipt> Receipts, List<CashDocumentRecord> Cash, List<string> Warnings);
public sealed record ManualNonCash(DateOnly Date, decimal Amount, DateTime ChangedAt);
public sealed class ExcelImportState
{
    public bool Enabled { get; set; }
    public List<ExcelBatch> Batches { get; set; } = [];
    public List<ManualNonCash> NonCash { get; set; } = [];
}

public sealed class ExcelImportStore
{
    private readonly string _path;
    public ExcelImportStore(string? path = null) => _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "excel-imports.json");
    public ExcelImportState Load() => File.Exists(_path)
        ? JsonSerializer.Deserialize<ExcelImportState>(File.ReadAllText(_path)) ?? new() : new();
    public void Save(ExcelImportState state) => AtomicJsonFile.Save(_path, state);
    // A file is a complete daily report, not an incremental list. Replace only dates present in this report.
    public static void Merge(ExcelImportState state, ExcelBatch batch)
    {
        var dates = batch.Dates.ToHashSet();
        state.Batches = state.Batches.Select(old => old.Kind != batch.Kind ? old : old with
        {
            Dates = old.Dates.Where(d => !dates.Contains(d)).ToList(),
            Sales = old.Sales.Where(r => !dates.Contains(r.Date)).ToList(),
            Receipts = old.Receipts.Where(r => !dates.Contains(r.Date)).ToList(),
            Cash = old.Cash.Where(r => !dates.Contains(r.Date)).ToList()
        }).Where(x => x.Dates.Count > 0).ToList();
        state.Batches.Add(batch);
        state.Enabled = true;
    }
}

public static class ExcelReportReader
{
    public static IReadOnlyList<string> Reconcile(ExcelImportState state)
    {
        var warnings = new List<string>();
        var cashDays = state.Batches.Where(x => x.Kind == "cash").SelectMany(x => x.Dates).ToHashSet();
        var salesDays = state.Batches.Where(x => x.Kind == "sales").SelectMany(x => x.Dates).ToHashSet();
        foreach (var day in cashDays.Intersect(salesDays))
        {
            var cash = state.Batches.SelectMany(x => x.Cash).Where(x => x.Date == day && x.Type.Contains("Վաճառք")).ToList();
            var sales = state.Batches.SelectMany(x => x.Sales).Where(x => x.Date == day).ToList();
            if (cash.Sum(x => x.Amount) != sales.Sum(x => x.Sales) || cash.Count != sales.Select(x => x.Document).Distinct().Count())
                warnings.Add($"{day:dd.MM.yyyy}․ դրամական և վաճառքի ֆայլերը չեն համընկնում․ {cash.Sum(x => x.Amount):N2} / {sales.Sum(x => x.Sales):N2} ֏, {cash.Count} / {sales.Select(x => x.Document).Distinct().Count()} փաստաթուղթ։");
        }
        return warnings;
    }
    public static ExcelBatch Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var shared = CashDocumentImportService.ReadSharedStrings(zip, ns);
        var rows = CashDocumentImportService.LoadXml(zip, "xl/worksheets/sheet1.xml").Descendants(ns + "row")
            .Select(r => CashDocumentImportService.ReadRow(r, ns, shared)).ToList();
        var hi = rows.FindIndex(r => r.Contains("Ամսաթիվ") && (r.Contains("Գումար") || r.Contains("Քանակ")));
        if (hi < 0) throw new InvalidOperationException("Հաշվետվության սյունակները չեն ճանաչվել։");
        var headers = rows[hi];
        var kind = headers.Contains("Ինք․ Գումար դրամով (Ներառյալ ԱԱՀ)") ? "sales" : headers.Contains("Դրամարկղ") ? "cash" : "warehouse";
        string Cell(List<string> row, string header) { var i = headers.IndexOf(header); return i >= 0 && i < row.Count ? row[i].Trim() : ""; }
        string At(List<string> row, int col) => col < row.Count ? row[col].Trim() : "";
        decimal Amount(List<string> r, string h) => CashDocumentImportService.ReadAmount(Cell(r, h))
            ?? throw new InvalidOperationException($"Թվային արժեք չկա՝ {h}, փաստաթուղթ {Cell(r, "Փաստաթղթի N")}։");
        var sales = new List<ExcelSale>(); var receipts = new List<ExcelReceipt>(); var cash = new List<CashDocumentRecord>();
        var warnings = new List<string>(); var dates = new HashSet<DateOnly>(); decimal? footer = null; decimal rawTotal = 0; int drafts = 0;
        for (var index = hi + 1; index < rows.Count; index++)
        {
            var row = rows[index];
            if (Cell(row, "Ամսաթիվ") == "Ընդամենը") { footer = CashDocumentImportService.ReadAmount(Cell(row, "Գումար")); continue; }
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var date = CashDocumentImportService.ReadDate(Cell(row, "Ամսաթիվ"))
                ?? throw new InvalidOperationException($"Տող {index + 1}․ ամսաթիվը չի ճանաչվել։");
            dates.Add(date);
            var doc = Cell(row, "Փաստաթղթի N");
            if (string.IsNullOrWhiteSpace(doc)) throw new InvalidOperationException($"Տող {index + 1}․ փաստաթղթի համարը բացակայում է։");
            if (kind != "sales")
            {
                var amount = decimal.Round(Amount(row, "Գումար"),2); rawTotal += amount;
                if (Cell(row, "Վիճակ") != "Գրանցված") { drafts++; warnings.Add($"Տող {index + 1} · {doc} · {Cell(row, "Վիճակ")} · {amount:N2} ֏ — չի ներառվել"); continue; }
                var type = Cell(row, "Տեսակ"); var supplier = Cell(row, "Գործընկերոջ անվանում");
                if (kind == "cash")
                {
                    if (!(type.Contains("Վաճառք") || type.Contains("Օրդեր") || type.Contains("օրդեր")))
                        throw new InvalidOperationException($"Չմշակված դրամական տեսակ՝ {type}։ Պետք է ճշտել ուղղությունը։");
                    cash.Add(new(date, doc, type, amount, Cell(row, "Տեղեկություն"),
                        string.IsNullOrWhiteSpace(supplier) ? Cell(row, "Տեղեկություն") : supplier,
                        Cell(row, "Դրամարկղ"), Cell(row, "Հերթափոխ"), Cell(row, "Աշխատակից")));
                }
                else
                {
                    if (!type.Contains("Պահեստի մուտքի օրդեր"))
                        throw new InvalidOperationException($"Չմշակված պահեստային տեսակ՝ {type}։ Վերադարձների նմուշը պետք է ստուգել նախքան ներմուծելը։");
                    if (string.IsNullOrWhiteSpace(supplier)) throw new InvalidOperationException($"Տող {index + 1}․ ստացման մատակարարը բացակայում է։");
                    receipts.Add(new(date, doc, supplier, Cell(row, "Գործընկեր"), Cell(row, "Պահեստ"), amount));
                }
            }
            else
            {
                if (Cell(row, "Գործողության տեսակ") != "Վաճառք (Կտրոն)")
                    throw new InvalidOperationException($"Տող {index + 1}․ վաճառքի/վերադարձի տեսակը պահանջում է առանձին ստուգում։");
                var sale = new ExcelSale(date, doc, Cell(row, "ՀԴՄ կտրոնի N"), Cell(row, "Կոդ"),
                    At(row, 6), At(row, 8), Cell(row, "Մատակարար"), Amount(row, "Քանակ"),
                    decimal.Round(Amount(row, "Վաճառքի գումար դրամով(Ներառյալ ԱԱՀ)"),2), decimal.Round(Amount(row, "Ինք․ Գումար դրամով (Ներառյալ ԱԱՀ)"),2),
                    At(row, 10), At(row, 29), Cell(row, "Հերթափոխի N"));
                sales.Add(sale);
                if (sale.Cost < 0 || sale.Sales > 0 && (sale.Cost <= sale.Sales * .01m || sale.Cost > sale.Sales))
                    warnings.Add($"Կասկածելի ինքնարժեք · տող {index + 1} · {sale.Product} · վաճառք {sale.Sales:N2}, ինքնարժեք {sale.Cost:N2} ֏");
                if (string.IsNullOrWhiteSpace(sale.Supplier)) warnings.Add($"Մատակարարը բացակայում է · տող {index + 1} · {sale.Product}");
            }
        }
        if (dates.Count == 0) throw new InvalidOperationException("Տվյալների տողեր չկան։");
        if (footer is not null && footer != rawTotal)
            warnings.Add($"Ընդամենը՝ {footer:N2}, տողերի գումար՝ {rawTotal:N2} ֏։ Կօգտագործվեն տողերը։");
        if (drafts > 0) warnings.Insert(0, $"Չգրանցված / սևագիր փաստաթղթեր՝ {drafts}․ բացառված են։");
        if (sales.Count != sales.Distinct().Count()) warnings.Add("Կան նույնական վաճառքային տողեր․ պահպանված են, ինքնաբերաբար չեն ջնջվում։");
        if (cash.GroupBy(x => (x.Date, x.DocumentNumber, x.Type, x.CashBox)).Any(g => g.Count() > 1) ||
            receipts.GroupBy(x => (x.Date, x.Document, x.Storage)).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Նույն փաստաթուղթը կրկնվում է ֆայլում․ նախ ճշտեք կրկնումները։");
        if (kind == "cash") warnings.Add("Կանխիկ/անկանխիկ բաժանումը բացակայում է․ յուրաքանչյուր օրվա համար ձեռքով նշեք անկանխիկ վաճառքը։");
        return new(kind, Path.GetFileName(path), DateTime.Now, dates.Order().ToList(), sales, receipts, cash, warnings);
    }
}
