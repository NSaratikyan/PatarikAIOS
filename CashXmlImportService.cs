using System.Globalization;
using System.Xml.Linq;

namespace PatarikAIOS;

/// <summary>Reads ArmSoft XML sales receipts (MTBill), including a file containing several receipts.</summary>
public static class CashXmlImportService
{
    public static IReadOnlyList<CashDocumentRecord> Import(string path)
    {
        var document = XDocument.Load(path);
        var rows = document.Descendants().Where(x => x.Name.LocalName == "MTBill")
            .Where(x => !string.Equals(Value(x, "IsDeleted"), "true", StringComparison.OrdinalIgnoreCase))
            .Select(x =>
            {
                var date = DateOnly.FromDateTime(DateTime.Parse(Value(x, "DocumentDate"), CultureInfo.InvariantCulture));
                var amount = decimal.Parse(FirstValue(x, "ReceivedAmount", "TotalAmount"), CultureInfo.InvariantCulture);
                return new CashDocumentRecord(date, Value(x, "DocumentNumber"), "Վաճառք (Կտրոն)", amount,
                    "XML վաճառքի կտրոն", string.Empty, Value(x, "CashDesk"), Value(x, "CashierShiftNumber"), Value(x, "SalesConsultant"));
            }).ToList();
        if (rows.Count == 0) throw new InvalidOperationException("XML ֆայլում վաճառքի կտրոն (MTBill) չգտնվեց։");
        return rows;
    }

    private static string Value(XElement element, string name) => element.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Value?.Trim() ?? string.Empty;
    private static string FirstValue(XElement element, params string[] names) => names.Select(x => Value(element, x)).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "0";
}
