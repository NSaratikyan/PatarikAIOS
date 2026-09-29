using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using System.Text.Json;

namespace PatarikAIOS;

/// <summary>
/// Local connection settings for HԾ-Առևտուր Cloud.
/// The API key stays in the current Windows user's AppData folder and is never included in source code.
/// </summary>
public sealed record HtsApiSettings(string BaseUrl, string ApiKey, string Language = "hy-AM")
{
    public static HtsApiSettings Empty => new("https://api.armsoft.am/trade/", "", "hy-AM");
    public bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed class HtsApiSettingsStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "hts-api-settings.json");

    public HtsApiSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<HtsApiSettings>(File.ReadAllText(_path)) ?? HtsApiSettings.Empty;
        }
        catch (JsonException) { }
        return HtsApiSettings.Empty;
    }

    public void Save(HtsApiSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record HtsConnectionTestResult(bool Success, string Message);

public sealed class HtsApiDataProvider(HtsApiSettings settings, Func<HttpClient>? clientFactory = null) : IHtsDataProvider, IBusinessSummaryProvider, IFundsMovementProvider, ICashDocumentProvider, IPurchasePlanningProvider, ISupplierSalesAnalysisProvider, ISupplierActivityProvider
{
    private readonly HtsApiSettings _settings = settings;
    private HttpClient NewClient() => clientFactory?.Invoke() ?? CreateClient(_settings);
    public string? LastCashWarning { get; private set; }

    public async Task<DashboardSnapshot> GetSnapshotAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        using var client = NewClient();
        var previousDate = date.AddDays(-1);
        var warnings = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var todaySalesTask = CaptureReportAsync(() => GetSalesRowsAsync(client, date, cancellationToken), warnings);
        var previousSalesTask = CaptureReportAsync(() => GetSalesRowsAsync(client, previousDate, cancellationToken), warnings);
        var balancesTask = CaptureReportAsync(() => GetPartnerBalancesAsync(client, date, cancellationToken), warnings);
        await Task.WhenAll(todaySalesTask, previousSalesTask, balancesTask);

        var todaySales = ToSalesSummary(todaySalesTask.Result ?? [], previousSalesTask.Result ?? []) with
        {
            SalesAvailable = todaySalesTask.Result is not null && HasAmounts(todaySalesTask.Result, "saleAmountWithVAT"),
            CostAvailable = todaySalesTask.Result is not null && HasAmounts(todaySalesTask.Result, "costAmountWithVAT"),
            ComparisonAvailable = previousSalesTask.Result is not null && HasAmounts(previousSalesTask.Result, "costAmountWithVAT") && HasAmounts(previousSalesTask.Result, "saleAmountWithVAT"),
            DataWarning = warnings.IsEmpty ? null : string.Join("\n", warnings)
        };
        var partnerRows = await GetSupplierPartnersSafeAsync(client, cancellationToken);
        var apiSuppliers = ToSuppliers(balancesTask.Result ?? [], partnerRows, date);
        var documentRows = await CaptureReportAsync(() => GetDocumentsAsync(client, date, date, cancellationToken), warnings) ?? [];
        todaySales = todaySales with { DataWarning = warnings.IsEmpty ? null : string.Join("\n", warnings) };
        var dailyMovements = ToSupplierDailyMovements(documentRows, apiSuppliers, date);

        return new DashboardSnapshot
        {
            Date = date,
            Cash = new CashPosition(0m, 0m),
            Suppliers = apiSuppliers,
            SupplierMovements = dailyMovements,
            // Payments are built from HTS documents, supplier plans and the
            // owner's required-payment database. Demo payments must never
            // appear while the live HTS connection is active.
            Payments = [],
            Forecast = [],
            Sales = todaySales,
            Recommendations = [],
            Tasks = []
        };
    }

    public async Task<BusinessSummary> GetBusinessSummaryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        using var client = NewClient();
        var days = endDate.DayNumber - startDate.DayNumber + 1;
        var previousStart = startDate.AddDays(-days);
        var previousEnd = startDate.AddDays(-1);
        var warnings = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var rowsTask = CaptureReportAsync(() => GetSalesRowsAsync(client, startDate, endDate, cancellationToken), warnings);
        var previousTask = CaptureReportAsync(() => GetSalesRowsAsync(client, previousStart, previousEnd, cancellationToken), warnings);
        var documentsTask = CaptureReportAsync(() => GetDocumentsAsync(client, startDate, endDate, cancellationToken), warnings);
        await Task.WhenAll(rowsTask, previousTask, documentsTask);

        var candidates = (documentsTask.Result ?? [])
            .Where(row => !string.IsNullOrWhiteSpace(Text(row, "partnerName")) && !string.IsNullOrWhiteSpace(Text(row, "storageName")))
            .ToList();
        var identifiedReceipts = candidates.Where(IsSupplyDocument).ToList();
        var receiptRows = identifiedReceipts;
        var supplies = receiptRows
            .GroupBy(row => new
            {
                Storage = Text(row, "storageName") ?? "Չնշված պահեստ",
                Production = IsProductionSupply(row)
            })
            .Select(group => new SupplyByStorage(group.Key.Storage, group.Sum(row => Number(row, "amount")), group.Key.Production))
            .ToList();

        var sales = ToSalesSummary(rowsTask.Result ?? [], previousTask.Result ?? []) with
        {
            SalesAvailable = rowsTask.Result is not null && HasAmounts(rowsTask.Result, "saleAmountWithVAT"),
            CostAvailable = rowsTask.Result is not null && HasAmounts(rowsTask.Result, "costAmountWithVAT"),
            ComparisonAvailable = previousTask.Result is not null && HasAmounts(previousTask.Result, "saleAmountWithVAT") && HasAmounts(previousTask.Result, "costAmountWithVAT"),
            DataWarning = warnings.IsEmpty ? null : string.Join("\n", warnings)
        };
        return new BusinessSummary(startDate, endDate, sales, [], supplies, 0m, 0m);
    }

    public async Task<BankSalesBreakdown> GetNonCashSalesAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        using var client = NewClient();
        var rows = await GetEcrChecksAsync(client, startDate, endDate, cancellationToken);
        decimal report = 0m, ameria = 0m, idram = 0m;
        foreach (var row in rows)
        {
            var amount = Number(row, "nonCashAmount");
            if (amount == 0m) continue;
            var payment = $"{Text(row, "paymentSystem")} {Text(row, "posPartnerName")}";
            if (payment.Contains("idram", StringComparison.OrdinalIgnoreCase)) idram += amount;
            else if (payment.Contains("ամերի", StringComparison.OrdinalIgnoreCase) || payment.Contains("ameria", StringComparison.OrdinalIgnoreCase) || payment.Contains("099", StringComparison.OrdinalIgnoreCase)) ameria += amount;
            else report += amount;
        }
        return new BankSalesBreakdown(report, ameria, idram);
    }

    public async Task<IReadOnlyList<CashDocumentRecord>> GetCashDocumentsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        using var client = NewClient();
        var warnings = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var documents = await CaptureReportAsync(() => GetDocumentsAsync(client, startDate, endDate, cancellationToken), warnings);
        var result = (documents ?? [])
            .Where(IsCashDocument)
            .Select(row => ToCashDocumentRecord(row, startDate))
            .Where(x => x.Amount > 0m && x.Date >= startDate && x.Date <= endDate)
            .ToList();
        // This feed contains only registered sales/returns, not cash orders.
        // Retain explicit zero days so a verified no-sale day differs from a failed load.
        var checks = await CaptureReportAsync(() => GetEcrChecksAsync(client, startDate, endDate, cancellationToken), warnings);
        if (checks is not null && !HasAmounts(checks, "cashAmount"))
        { warnings.Enqueue("ՀԾ կտրոններում կանխիկ վճարման գումարը բացակայում է։"); checks = null; }
        LastCashWarning = warnings.IsEmpty ? null : string.Join("\n", warnings);
        if (documents is null && checks is null) throw new InvalidOperationException(LastCashWarning);
        for (var date = startDate; checks is not null && date <= endDate; date = date.AddDays(1))
        {
            var cash = checks.Where(x => DocumentDate(x) == date).Sum(x => Number(x, "cashAmount"));
            result.Add(new CashDocumentRecord(date, $"HTS-CASH-SALES-{date:yyyyMMdd}", "ecr-cash-sales", cash,
                "ՀԴՄ կանխիկ վաճառք՝ հանած կանխիկ վերադարձները", "", "0001", "", ""));
        }
        return result;
    }

    public async Task<IReadOnlyList<PurchaseProposal>> GetPurchaseProposalsAsync(
        DateOnly stockDate,
        DateOnly deliveryDate,
        IReadOnlyList<string> scheduledSuppliers,
        IReadOnlyDictionary<string, int> supplierCoverageDays,
        CancellationToken cancellationToken = default)
    {
        if (scheduledSuppliers.Count == 0) return [];
        using var client = NewClient();
        var balanceRowsTask = GetProductsBalancesAsync(client, stockDate, cancellationToken);
        var salesRowsTask = GetSalesRowsAsync(client, stockDate.AddDays(-6), stockDate, cancellationToken);
        await Task.WhenAll(balanceRowsTask, salesRowsTask);
        var rows = balanceRowsTask.Result;
        var salesBySupplierAndProduct = salesRowsTask.Result
            .Select(row => new
            {
                Supplier = Text(row, "partySupplierName") ?? string.Empty,
                Product = Text(row, "itemName") ?? Text(row, "name") ?? string.Empty,
                Quantity = Number(row, "quantity")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Supplier) && !string.IsNullOrWhiteSpace(x.Product))
            .GroupBy(x => $"{SupplierKey(x.Supplier)}|{ProductKey(x.Product)}")
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity));
        // The owner asked for a small monetary safety reserve (about 1–2k AMD)
        // per supplier order, not a retail-price based uplift.  The reserve is
        // divided across that supplier's candidate products and then converted
        // into units using the HTS receipt/unit cost.
        var productCountBySupplier = rows
            .Select(row => Text(row, "partySupplierName") ?? string.Empty)
            .Where(supplier => scheduledSuppliers.Any(x => SupplierMatches(x, supplier)))
            .GroupBy(SupplierKey)
            .ToDictionary(group => group.Key, group => Math.Max(1, group.Count()));
        return rows.Select(row =>
        {
            var supplier = Text(row, "partySupplierName") ?? string.Empty;
            var plannedSupplier = scheduledSuppliers.FirstOrDefault(x => SupplierMatches(x, supplier));
            if (plannedSupplier is null) return null;

            var quantity = Number(row, "quantity");
            var minimum = Number(row, "minimumQuantity");
            var maximum = Number(row, "maximumQuantity");
            var productForSales = Text(row, "name") ?? Text(row, "fullName") ?? string.Empty;
            var salesQuantity = salesBySupplierAndProduct.GetValueOrDefault($"{SupplierKey(supplier)}|{ProductKey(productForSales)}");
            var coverageDays = Math.Clamp(supplierCoverageDays.GetValueOrDefault(plannedSupplier, 3), 1, 30);
            var unitCost = Number(row, "costPriceWithVAT");
            if (unitCost <= 0m && quantity > 0m) unitCost = Number(row, "costAmountWithVAT") / quantity;
            var supplierProductCount = productCountBySupplier.GetValueOrDefault(SupplierKey(supplier), 1);
            var safetyAmountPerProduct = 1_500m / supplierProductCount;
            var safetyQuantity = unitCost > 0m ? Math.Ceiling(safetyAmountPerProduct / unitCost) : 0m;
            // The proposal starts from recent sales at supplier cost, adds the
            // shared 1,500 AMD safety margin, then deducts physical stock.
            // It never uses retail selling prices.
            var salesBasedQuantity = Math.Max(0m, salesQuantity + safetyQuantity - quantity);
            var proposed = Number(row, "orderQuantity");
            proposed = Math.Max(proposed, salesBasedQuantity);
            if (proposed <= 0m && minimum > 0m && quantity <= minimum)
                proposed = Math.Max(0m, (maximum > minimum ? maximum : minimum * 2m) - quantity);
            if (proposed <= 0m) return null;

            var product = Text(row, "name") ?? Text(row, "fullName") ?? "Ապրանք";
            var storage = Text(row, "storageName") ?? "Պահեստ";
            var unit = Text(row, "unitMeasureAbbreviation") ?? Text(row, "unitMeasure") ?? "հատ";
            var reason = quantity <= minimum
                ? $"Մնացորդ՝ {quantity:0.##}, նվազագույն սահման՝ {minimum:0.##}"
                : "ՀԾ-ի առաջարկվող պատվերի քանակ";
            if (salesQuantity > 0m)
                reason = $"Վերջին 7 օր վաճառք՝ {salesQuantity:0.##}; պաշար՝ մինչև հաջորդ {coverageDays} օրվա մատակարարումը";
            return new PurchaseProposal(deliveryDate, plannedSupplier, product, storage, unit, quantity, minimum, maximum, proposed, unitCost, reason);
        }).Where(x => x is not null).Cast<PurchaseProposal>()
          .OrderBy(x => x.Supplier).ThenBy(x => x.Storage).ThenBy(x => x.Product).ToList();
    }

    public async Task<IReadOnlyList<SupplierSalesAnalysis>> GetSupplierSalesAnalysisAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        using var client = NewClient();
        var rows = await GetSalesRowsAsync(client, startDate, endDate, cancellationToken);
        var partners = await GetSupplierPartnersSafeAsync(client, cancellationToken);
        var namesById = partners.Where(x => !string.IsNullOrWhiteSpace(Text(x, "name")) && Text(x, "id") is not null)
            .GroupBy(x => Text(x, "id")!).ToDictionary(x => x.Key, x => Text(x.First(), "name")!);

        return rows
            .Select(row => new
            {
                Supplier = TextAny(row, "partySupplierName")?.Trim()
                    ?? (namesById.TryGetValue(Text(row, "partySupplierId") ?? "", out var name) ? name : "Չկապակցված մատակարար"),
                Product = Text(row, "itemName") ?? Text(row, "name") ?? string.Empty,
                Storage = Text(row, "storageName") ?? string.Empty,
                Sales = Number(row, "saleAmountWithVAT"),
                Cost = Number(row, "costAmountWithVAT"),
                Quantity = Number(row, "quantity"),
                CostAvailable = HasAmounts([row], "costAmountWithVAT")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Supplier))
            .GroupBy(x => x.Supplier!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SupplierSalesAnalysis(
                group.Key,
                group.Sum(x => x.Sales),
                group.Sum(x => x.Cost),
                group.Sum(x => x.Quantity),
                group.Select(x => x.Product).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                group.Select(x => x.Storage).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                group.All(x => x.CostAvailable)))
            .OrderByDescending(x => x.SalesAmount)
            .ToList();
    }

    public async Task<IReadOnlyList<SupplierActivityLine>> GetSupplierActivityAsync(
        string supplier, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        using var client = NewClient();
        var documents = await GetDocumentsAsync(client, startDate, endDate, cancellationToken);
        const decimal matchingTolerance = 10m;

        return documents
            .Select(row => new { Row = row, Partner = Text(row, "partnerName") ?? Text(row, "partner") ?? string.Empty })
            .Where(item => SupplierMatches(item.Partner, supplier))
            .Where(item => IsSupplyDocument(item.Row) || IsSupplierPaymentDocument(item.Row))
            .GroupBy(item => DocumentDate(item.Row) ?? startDate)
            .Select(group =>
            {
                var receipts = group.Where(item => IsSupplyDocument(item.Row)).Sum(item => Math.Abs(Number(item.Row, "amount")));
                var payments = group.Where(item => IsSupplierPaymentDocument(item.Row)).Sum(item => Math.Abs(Number(item.Row, "amount")));
                var paymentForOrder = receipts > 0m && payments > 0m && Math.Abs(receipts - payments) <= matchingTolerance ? payments : 0m;
                var oldDebtPayment = paymentForOrder == 0m ? payments : 0m;
                var documentsText = string.Join(", ", group.Select(item => Text(item.Row, "number") ?? Text(item.Row, "documentNumber") ?? "—").Distinct());
                var description = string.Join("; ", group.Select(item => Text(item.Row, "typeName") ?? Text(item.Row, "type") ?? "Փաստաթուղթ").Distinct());
                return new SupplierActivityLine(group.Key, receipts, paymentForOrder, oldDebtPayment, documentsText, description);
            })
            .OrderBy(item => item.Date)
            .ToList();
    }

    public static async Task<HtsConnectionTestResult> TestConnectionAsync(HtsApiSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
            return new(false, "Լրացրեք ՀԾ API հասցեն և API բանալին։");

        using var client = CreateClient(settings);

        // Test the same read-only reports used by the desktop application. Testing a
        // barcode directory can fail even when the report permissions are available.
        // No HԾ documents are created or changed by these probes.
        var day = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        using var balances = await PostJsonAsync(client, "v1/reports/partnersbalances", new
        {
            pageSize = 1,
            date = day,
            openedByContracts = false
        }, cancellationToken);
        using var sales = await PostJsonAsync(client, "v1/reports/salesanalysis", new
        {
            pageSize = 1,
            startDate = day,
            endDate = day,
            showSumsWithVAT = true,
            showParties = true,
            showCostAndSalePrices = true
        }, cancellationToken);
        using var documents = await PostJsonAsync(client, "v1/journals/alldocuments", new
        {
            pageSize = 1,
            startDate = day,
            endDate = day
        }, cancellationToken);

        static bool Ok(HttpResponseMessage response) => (int)response.StatusCode is >= 200 and < 300;
        var balancesOk = Ok(balances);
        var salesOk = Ok(sales);
        var documentsOk = Ok(documents);
        var status = $"Պարտքեր՝ {(int)balances.StatusCode}; Վաճառք՝ {(int)sales.StatusCode}; Փաստաթղթեր՝ {(int)documents.StatusCode}.";

        if (balancesOk && salesOk)
        {
            var documentsNote = documentsOk
                ? " Պարտքերի և վաճառքի տվյալների հասանելիությունը հաստատված է։"
                : " Պարտքերի և վաճառքի հասանելիությունը հաստատված է, բայց փաստաթղթերի մատյանը դեռ հասանելի չէ։";
            return new(true, "ՀԾ API կապը հաջող է։ " + status + documentsNote);
        }

        var hasAccessError = new[] { balances.StatusCode, sales.StatusCode, documents.StatusCode }
            .Any(code => code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        var nextStep = hasAccessError
            ? " API բանալու համար պետք է բացվեն Reports → Partners balances և Reports → Sales analysis իրավունքները։"
            : " Ստուգեք API հասցեն և API բանալին, ապա կրկին փորձեք։";
        return new(false, "ՀԾ API-ի անհրաժեշտ հաշվետվությունները դեռ հասանելի չեն։ " + status + nextStep);
        /*

        // ArmSoft documents this endpoint as the one resource that does not require a key.
        // If the server version differs, the authenticated lightweight barcode call provides
        // a useful second verification without changing any HԾ data.
        var version = await SendAsync(client, "api/Version", cancellationToken);
        if (version.StatusCode is HttpStatusCode.OK)
            return new(true, "Կապը հաջող է։ ՀԾ API-ն հասանելի է։");

        var barcode = await PostJsonAsync(client, "v1/directories/barcodes/list", new { pageSize = 1 }, cancellationToken);
        if (barcode.StatusCode is HttpStatusCode.OK)
            return new(true, "Կապը հաջող է։ ՀԾ API բանալին ընդունվել է։");

        if (barcode.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ||
            version.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new(false, "Բանալին սխալ է կամ ընտրված ՀԾ օգտագործողը API իրավունք չունի։");

        return new(false, $"ՀԾ API-ն պատասխանեց {((int)barcode.StatusCode)} կոդով։ Ստուգեք հասցեն, բանալին և API իրավասությունները։");
    }

        */
    }

    private static HttpClient CreateClient(HtsApiSettings settings)
    {
        var baseUrl = settings.BaseUrl.TrimEnd('/') + "/";
        var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Add("apiKey", settings.ApiKey.Trim());
        client.DefaultRequestHeaders.Add("Accept-Language", settings.Language);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string route, CancellationToken cancellationToken)
    {
        try { return await client.GetAsync(route, cancellationToken); }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("Չհաջողվեց միանալ ՀԾ API հասցեին։ Համոզվեք, որ ինտերնետը հասանելի է։", exception);
        }
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string route, object body, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        try { return await client.PostAsync(route, content, cancellationToken); }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("Չհաջողվեց միանալ ՀԾ API հասցեին։ Համոզվեք, որ ինտերնետը հասանելի է։", exception);
        }
    }

    private static async Task<List<JsonElement>> GetSalesRowsAsync(HttpClient client, DateOnly date, CancellationToken cancellationToken)
        => await GetSalesRowsAsync(client, date, date, cancellationToken);

    private static async Task<List<JsonElement>> GetSalesRowsAsync(HttpClient client, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        var response = await PostJsonAsync(client, "v1/reports/salesanalysis", new
        {
            pageSize = 10000,
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd"),
            showSumsWithoutVAT = false,
            showSumsWithVAT = true,
            showCostAndSalePrices = true,
            showParties = true,
            showDiscounts = true
        }, cancellationToken);
        return await ReadRowsAsync(client, response, "վաճառքի վերլուծություն", cancellationToken);
    }

    private static async Task<List<JsonElement>> GetEcrChecksAsync(HttpClient client, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        var response = await PostJsonAsync(client, "v1/journals/ecrchecks", new
        {
            pageSize = 10000,
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd"),
            showPayments = true,
            documentTypes = new[] { "18", "15", "38" },
            documentState = 2,
            showOutputAmountsAsNegatives = true
        }, cancellationToken);
        return await ReadRowsAsync(client, response, "ՀԴՄ կտրոններ", cancellationToken);
    }

    private static async Task<List<JsonElement>> GetDocumentsAsync(HttpClient client, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        var response = await PostJsonAsync(client, "v1/journals/alldocuments", new
        {
            pageSize = 10000,
            documentState = 2,
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd")
        }, cancellationToken);
        return await ReadRowsAsync(client, response, "փաստաթղթերի մատյան", cancellationToken);
    }

    private static async Task<List<JsonElement>> GetProductsBalancesAsync(HttpClient client, DateOnly date, CancellationToken cancellationToken)
    {
        var response = await PostJsonAsync(client, "v1/reports/productsbalances", new
        {
            pageSize = 10000,
            date = date.ToString("yyyy-MM-dd")
        }, cancellationToken);
        return await ReadRowsAsync(client, response, "ապրանքների մնացորդներ", cancellationToken);
    }

    private static async Task<List<JsonElement>> GetPartnerBalancesAsync(HttpClient client, DateOnly date, CancellationToken cancellationToken)
    {
        var response = await PostJsonAsync(client, "v1/reports/partnersbalances", new
        {
            pageSize = 1000,
            date = date.ToString("yyyy-MM-dd"),
            openedByContracts = false
        }, cancellationToken);
        return await ReadRowsAsync(client, response, "գործընկերների մնացորդներ", cancellationToken);
    }

    private static async Task<List<JsonElement>> GetSupplierPartnersSafeAsync(HttpClient client, CancellationToken cancellationToken)
    {
        // The directory includes suppliers with neither debt nor a receipt on
        // the selected day. A missing optional permission falls back safely to
        // the balance report, without interrupting the desktop application.
        try
        {
            var response = await PostJsonAsync(client, "v1/directories/partners/list", new { pageSize = 10000, extended = false }, cancellationToken);
            return await ReadRowsAsync(client, response, "մատակարարների ցանկ", cancellationToken);
        }
        catch { return []; }
    }

    private static async Task<List<JsonElement>> ReadRowsAsync(HttpClient client, HttpResponseMessage response, string reportName, CancellationToken cancellationToken)
    {
        var rows = new List<JsonElement>();
        var nextUrl = response.RequestMessage?.RequestUri?.ToString().TrimEnd('/') + "/nextpage";
        for (var page = 0; page < 1000; page++)
        {
            JsonElement? nextId = null;
            using (response)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"ՀԾ API․ «{reportName}» հաշվետվությունը չբացվեց ({(int)response.StatusCode})։ Ստուգեք API օգտագործողի իրավունքները։");
                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;
                if (!TryGetProperty(root, "data", out var data) || data.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException($"ՀԾ․ «{reportName}» պատասխանի կառուցվածքը ճանաչելի չէ։");
                rows.AddRange(data.EnumerateArray().Select(x => x.Clone()));
                if (!Bool(root, "hasMore")) return rows;
                if (data.GetArrayLength() == 0 || !TryGetProperty(root, "id", out var id))
                    throw new InvalidOperationException($"ՀԾ․ «{reportName}» հաջորդ էջը հնարավոր չէ բեռնել։");
                nextId = id.Clone();
            }
            response = await PostJsonAsync(client, nextUrl!, new { id = nextId.Value, close = false }, cancellationToken);
        }
        response.Dispose();
        throw new InvalidOperationException($"ՀԾ․ «{reportName}» հաշվետվության էջավորումը չի ավարտվել։");
    }

    private static SalesSummary ToSalesSummary(IReadOnlyList<JsonElement> rows, IReadOnlyList<JsonElement> previousRows)
    {
        decimal Amount(IEnumerable<JsonElement> source, string property) => source.Sum(row => Number(row, property));
        int Checks(IEnumerable<JsonElement> source) => source
            .Select(row => TextAny(row, "isn") ?? (TextAny(row, "ecrCheckNumber", "documentNumber") is { } number ? $"{Text(row, "date")}|{Text(row, "cashDesk")}|{number}" : null))
            .Where(x => x is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();

        var sales = Amount(rows, "saleAmountWithVAT");
        var cost = Amount(rows, "costAmountWithVAT");
        var previousSales = Amount(previousRows, "saleAmountWithVAT");
        var previousCost = Amount(previousRows, "costAmountWithVAT");
        return new SalesSummary(sales, cost, Checks(rows), previousSales, previousSales - previousCost, Checks(previousRows),
            HasAmounts(rows, "saleAmountWithVAT"), HasAmounts(rows, "costAmountWithVAT"),
            HasAmounts(previousRows, "saleAmountWithVAT") && HasAmounts(previousRows, "costAmountWithVAT"));
    }

    private static bool HasAmounts(IEnumerable<JsonElement> rows, string property) => rows.All(row =>
        TryGetProperty(row, property, out var value) && (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _) ||
        value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)));

    private static async Task<List<JsonElement>?> CaptureReportAsync(Func<Task<List<JsonElement>>> load,
        System.Collections.Concurrent.ConcurrentQueue<string> warnings)
    {
        try { return await load(); }
        catch (Exception ex) when (ex is not OperationCanceledException) { warnings.Enqueue(ex.Message); return null; }
    }

    private static List<Supplier> ToSuppliers(IReadOnlyList<JsonElement> rows, DateOnly date) => rows
        .Where(row => !string.IsNullOrWhiteSpace(Text(row, "name")))
        .Select(row => new Supplier(
            Name: Text(row, "name")!,
            Direction: Text(row, "contractName") ?? "ՀԾ գործընկեր",
            Debt: Math.Abs(Number(row, "balance")),
            NextSupplyDate: date,
            PriorityScore: 50,
            PaymentMode: Text(row, "currency") ?? "AMD",
            Note: "ՀԾ API մնացորդ"))
        .OrderByDescending(x => x.Debt)
        .ToList();

    private static List<Supplier> ToSuppliers(IReadOnlyList<JsonElement> balanceRows, IReadOnlyList<JsonElement> partnerRows, DateOnly date)
    {
        var balances = balanceRows.Where(x => !string.IsNullOrWhiteSpace(Text(x, "name")))
            .GroupBy(x => SupplierKey(Text(x, "name")!))
            .ToDictionary(x => x.Key, x => x.First());
        var result = partnerRows
            .Where(row => Bool(row, "supplier") && !string.IsNullOrWhiteSpace(Text(row, "name")))
            .Select(row =>
            {
                var name = Text(row, "name")!;
                var hasBalance = balances.TryGetValue(SupplierKey(name), out var balance);
                var direction = hasBalance ? Text(balance, "contractName") : null;
                var currency = hasBalance ? Text(balance, "currency") : null;
                var debt = hasBalance ? Math.Abs(Number(balance, "balance")) : 0m;
                return new Supplier(name, direction ?? Text(row, "groupName") ?? "Մատակարար",
                    debt, date, 50, currency ?? "AMD", "ՀԾ API բազա");
            }).ToList();
        foreach (var row in balanceRows.Where(row => !string.IsNullOrWhiteSpace(Text(row, "name"))))
        {
            var name = Text(row, "name")!;
            if (result.Any(x => SupplierKey(x.Name) == SupplierKey(name))) continue;
            result.Add(new Supplier(name, Text(row, "contractName") ?? "Մատակարար", Math.Abs(Number(row, "balance")), date, 50, Text(row, "currency") ?? "AMD", "ՀԾ API մնացորդ"));
        }
        return result.OrderBy(x => x.Name).ToList();
    }

    private static List<SupplierDailyMovement> ToSupplierDailyMovements(IReadOnlyList<JsonElement> documents, IReadOnlyList<Supplier> suppliers, DateOnly date)
    {
        const decimal matchingTolerance = 10m;
        var supplierByKey = suppliers.GroupBy(x => SupplierKey(x.Name)).ToDictionary(x => x.Key, x => x.First());
        var operations = documents
            .Select(row => new { Row = row, Partner = Text(row, "partnerName") ?? Text(row, "partner") ?? string.Empty })
            .Where(x => !string.IsNullOrWhiteSpace(x.Partner) && supplierByKey.ContainsKey(SupplierKey(x.Partner)))
            .Select(x => new { x.Row, Supplier = supplierByKey[SupplierKey(x.Partner)] })
            .Where(x => IsSupplyDocument(x.Row) || IsSupplierPaymentDocument(x.Row))
            .ToList();

        return operations.GroupBy(x => x.Supplier.Name).Select(group =>
        {
            var receipts = group.Where(x => IsSupplyDocument(x.Row)).Sum(x => Math.Abs(Number(x.Row, "amount")));
            var payments = group.Where(x => IsSupplierPaymentDocument(x.Row)).Sum(x => Math.Abs(Number(x.Row, "amount")));
            var isPaymentForTodayReceipt = receipts > 0m && payments > 0m && Math.Abs(receipts - payments) <= matchingTolerance;
            var paymentForOrder = isPaymentForTodayReceipt ? payments : 0m;
            var oldDebtPayment = isPaymentForTodayReceipt ? 0m : payments;
            var supplier = group.First().Supplier;
            var closingDebt = supplier.Debt;
            var openingDebt = Math.Max(0m, closingDebt - receipts + payments);
            var descriptions = string.Join("; ", group.Select(x => $"{Text(x.Row, "typeName") ?? Text(x.Row, "type") ?? "Փաստաթուղթ"} №{Text(x.Row, "number") ?? Text(x.Row, "documentNumber") ?? "—"}").Distinct());
            var note = payments == 0m ? "Վճարում չկա" : isPaymentForTodayReceipt ? "Վճարումը համընկնում է տվյալ օրվա մատակարարմանը" : "Վճարումը գրանցվել է որպես հին պարտքի վճարում";
            return new SupplierDailyMovement(date, supplier.Name, supplier.Direction, openingDebt, receipts, paymentForOrder, oldDebtPayment,
                oldDebtPayment > 0m ? date.ToString("dd.MM.yyyy") : null, $"{note}. {descriptions}");
        }).OrderBy(x => x.Supplier).ToList();
    }

    private static decimal Number(JsonElement item, string property)
    {
        if (!TryGetProperty(item, property, out var value)) return 0m;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var number) => number,
            _ => 0m
        };
    }

    private static string? Text(JsonElement item, string property) =>
        TryGetProperty(item, property, out var value) ? value.ValueKind switch
        { JsonValueKind.String => value.GetString(), JsonValueKind.Number => value.GetRawText(), _ => null } : null;

    private static bool Bool(JsonElement item, string property) =>
        TryGetProperty(item, property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();

    private static bool TryGetProperty(JsonElement item, string property, out JsonElement value)
    {
        if (item.TryGetProperty(property, out value)) return true;
        if (item.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in item.EnumerateObject())
            {
                if (string.Equals(candidate.Name, property, StringComparison.OrdinalIgnoreCase))
                {
                    value = candidate.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string? TextAny(JsonElement item, params string[] properties) =>
        properties.Select(property => Text(item, property)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static decimal NumberAny(JsonElement item, params string[] properties) =>
        properties.Select(property => Number(item, property)).FirstOrDefault(value => value != 0m);

    private static string SupplierKey(string value) => new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string ProductKey(string value) => new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static bool SupplierMatches(string first, string second)
    {
        var a = SupplierKey(first); var b = SupplierKey(second);
        return a == b || (Math.Min(a.Length, b.Length) >= 6 && (a.Contains(b) || b.Contains(a)));
    }

    private static bool IsSupplyDocument(JsonElement row)
    {
        var name = Text(row, "typeName") ?? string.Empty;
        var type = Text(row, "type") ?? string.Empty;
        if (type is "2" or "02" or "3" or "03" || type.Contains("cash", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("օրդեր", StringComparison.OrdinalIgnoreCase) || name.Contains("դրամարկղ", StringComparison.OrdinalIgnoreCase)) return false;
        if ((name.Contains("մուտք", StringComparison.OrdinalIgnoreCase) && !name.Contains("գումարի", StringComparison.OrdinalIgnoreCase)) ||
            name.Contains("ստացում", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("storageinput", StringComparison.OrdinalIgnoreCase)) return true;
        return name.Contains("մուտք", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("ստացում", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("input", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("receipt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupplierPaymentDocument(JsonElement row)
    {
        var name = Text(row, "typeName") ?? string.Empty;
        var type = Text(row, "type") ?? string.Empty;
        return name.Contains("ելքի օրդեր", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("գումարի ելք", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("վճարում", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("cashoutput", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("payment", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("cash output", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCashDocument(JsonElement row)
    {
        return CanonicalCashDocumentType(row) is not null;
    }

    private static CashDocumentRecord ToCashDocumentRecord(JsonElement row, DateOnly fallbackDate)
    {
        var canonicalType = CanonicalCashDocumentType(row) ?? string.Empty;
        var visibleType = TextAny(row, "typeName", "documentTypeName", "type", "documentType") ?? canonicalType;
        var information = TextAny(row, "description", "note", "information", "comment", "purpose") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(visibleType) &&
            !information.Contains(visibleType, StringComparison.OrdinalIgnoreCase))
            information = string.IsNullOrWhiteSpace(information) ? visibleType : $"{visibleType}. {information}";

        // Keep the canonical marker in Type. CashDocumentImportService uses it
        // for reliable accounting even when the displayed Armenian name differs
        // between ArmSoft installations.
        return new CashDocumentRecord(
            DocumentDate(row) ?? fallbackDate,
            TextAny(row, "number", "documentNumber", "documentNo", "isn", "id") ?? Guid.NewGuid().ToString("N"),
            $"{visibleType} | {canonicalType}",
            Math.Abs(NumberAny(row, "amount", "documentAmount", "totalAmount", "sum", "outputAmount", "inputAmount")),
            information,
            TextAny(row, "partnerName", "partner", "recipientName", "recipient", "counterpartyName") ?? string.Empty,
            TextAny(row, "cashDeskCode", "cashDesk", "cashbox", "cashBox", "cashRegisterCode") ?? "0001",
            TextAny(row, "shift", "shiftNumber") ?? string.Empty,
            TextAny(row, "employeeName", "userName", "authorName") ?? string.Empty);
    }

    private static string? CanonicalCashDocumentType(JsonElement row)
    {
        var type = string.Join(" ", new[] { "type", "typeName", "documentType", "documentTypeName" }.Select(x => Text(row, x)));
        var information = TextAny(row, "description", "note", "information", "comment", "purpose") ?? string.Empty;
        var value = $"{type} {information}".ToLowerInvariant();

        if (value.Contains("cashtransfer") || value.Contains("գումարի ելք դեպի այլ դրամարկղ") ||
            value.Contains("դրամարկղի մնացորդի փակում")) return "cashtransfer";
        if (Text(row, "type") is "2" or "02") return "cashinput";
        if (Text(row, "type") is "3" or "03") return "cashoutput";
        if (value.Contains("cashinput") || value.Contains("մուտքի օրդեր") ||
            value.Contains("գումարի մուտք") || value.Contains("դրամարկղ մուտք")) return "cashinput";
        if (value.Contains("cashoutput") || value.Contains("ելքի օրդեր") ||
            value.Contains("գումարի ելք") || value.Contains("դրամարկղ ելք")) return "cashoutput";
        return null;
    }

    private static DateOnly? DocumentDate(JsonElement row)
    {
        var value = TextAny(row, "date", "documentDate", "operationDate", "dateCreated");
        if (DateOnly.TryParse(value, out var date)) return date;
        if (DateTime.TryParse(value, out var dateTime)) return DateOnly.FromDateTime(dateTime);
        return null;
    }

    private static bool IsProductionSupply(JsonElement row)
    {
        var partner = Text(row, "partnerName") ?? string.Empty;
        var storage = Text(row, "storageName") ?? string.Empty;
        return partner.Contains("Պատառիկ", StringComparison.OrdinalIgnoreCase) ||
               storage.Contains("արտադր", StringComparison.OrdinalIgnoreCase);
    }
}
