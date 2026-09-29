namespace PatarikAIOS;

public partial class MainWindow
{
    private readonly OperationsStateStore _operationsStateStore = new();
    private readonly Dictionary<string, string[]> _supplierInputDrafts = new();
    private string? _supplierSaveStatus;

    private IReadOnlyList<string> KnownSupplierNames() => _supplierWeekRows.Select(x => x.Supplier)
        .Concat(_snapshot?.Suppliers.Select(x => x.Name) ?? [])
        .Concat(_partnerDebts.Select(x => x.Supplier)).Concat(_deliveryPatterns.Select(x => x.Supplier))
        .Concat(_excelImportStore.Load().Batches.SelectMany(x => x.Receipts).Select(x => x.Supplier))
        .Concat(_excelImportStore.Load().Batches.SelectMany(x => x.Sales).Select(x => x.Supplier))
        .Concat(Enumerable.Range(0, 7).SelectMany(i => SupplierWeekPlanSeed.ForDate(_selectedDate.AddDays(i))).Select(x => x.Supplier))
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => ApprovedSupplierDirectory.Canonical(x) ?? x).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private string? ExactSupplier(string input)
    {
        if (ApprovedSupplierDirectory.Canonical(input) is { } approved) return approved;
        var key = SupplierNameSuggestions.Key(input);
        if (_operationsStateStore.Load().SupplierAliases.TryGetValue(key, out var canonical)) return canonical;
        var matches = KnownSupplierNames().Where(x => SupplierNameSuggestions.Key(x) == key).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task SendSupplierReviewAsync(TelegramBotSettings settings, PendingEmployeeOrderChange change)
    {
        var buttons = new List<IReadOnlyList<TelegramInlineButton>>();
        var suggestions = change.SuggestedSuppliers ?? [];
        for (var i = 0; i < suggestions.Count; i++)
            buttons.Add([new TelegramInlineButton("Ընտրել՝ " + suggestions[i], $"supchoose:{change.Id:N}:{i}")]);
        buttons.Add([new TelegramInlineButton("✏ Ուղղել անունը", $"supedit:{change.Id:N}")]);
        buttons.Add([new TelegramInlineButton("Նոր մատակարար է", $"supnew:{change.Id:N}"), new TelegramInlineButton("Մերժել", $"emporderreject:{change.Id}")]);
        await TelegramBotClient.SendMessageAsync(settings,
            $"⚠ Նոր մատակարար կամ անվան սխալ\n{change.Date:dd.MM.yyyy} · {change.Supplier}\nԱշխատակից՝ {change.ReportedByName}\nՊատվեր/վճարում/հին պարտքի վճարում՝ {change.ActualOrder:N0}/{change.ActualPayment:N0}/{change.ActualOldDebtPayment:N0}\n\nՆախ ճշտեք անունը։ Գումարները դեռ չեն գրանցվել։", buttons);
    }

    private PendingEmployeeOrderChange ResolveSupplier(PendingEmployeeOrderChange change, string canonical)
    {
        var baseline = PlannedSuppliersFor(change.Date).FirstOrDefault(x => string.Equals(x.Supplier, canonical, StringComparison.OrdinalIgnoreCase));
        var resolved = change with { Supplier = canonical, RequiresSupplierReview = false,
            PlannedOrder = baseline?.OrderAmount ?? 0m, PlannedPayment = baseline?.PaymentAmount ?? 0m,
            PlannedOldDebtPayment = baseline?.OldDebtPayment ?? 0m };
        var all = _pendingEmployeeOrderChangeStore.Load();
        var index = all.FindIndex(x => x.Id == change.Id);
        if (index < 0) throw new InvalidOperationException("Գրանցումն արդեն մշակված է։");
        all[index] = resolved; _pendingEmployeeOrderChangeStore.Save(all);
        var state = _operationsStateStore.Load();
        // Only a director-confirmed mapping becomes an alias. Fuzzy suggestions never merge records automatically.
        state.SupplierAliases[SupplierNameSuggestions.Key(change.OriginalSupplier ?? change.Supplier)] = canonical;
        state.PendingSupplierCorrection = null; _operationsStateStore.Save(state);
        return resolved;
    }

    private async Task<bool> HandleSupplierReviewButtonAsync(TelegramBotSettings settings, string data)
    {
        var parts = data.Split(':');
        if (parts.Length < 2 || parts[0] is not ("supchoose" or "supedit" or "supnew")) return false;
        if (!Guid.TryParse(parts[1], out var id)) return true;
        var change = _pendingEmployeeOrderChangeStore.Load().FirstOrDefault(x => x.Id == id);
        if (change is null || !change.RequiresSupplierReview)
        { await TelegramBotClient.SendMessageAsync(settings, "Այս անունն արդեն մշակված է։ Թարմացրեք հաստատումների ցանկը։"); return true; }
        if (parts[0] == "supedit")
        {
            _ownerPendingEmployeeIssueReplyStore.Save([]);
            var state = _operationsStateStore.Load(); state.PendingSupplierCorrection = id; _operationsStateStore.Save(state);
            await TelegramBotClient.SendMessageAsync(settings, "Գրեք բազայում առկա ճիշտ մատակարարի անունը։ Չեղարկելու համար՝ /cancel։");
            return true;
        }
        string canonical;
        if (parts[0] == "supnew") canonical = change.Supplier;
        else if (parts.Length == 3 && int.TryParse(parts[2], out var index) && index >= 0 && index < (change.SuggestedSuppliers?.Count ?? 0)) canonical = change.SuggestedSuppliers![index];
        else return true;
        var resolved = ResolveSupplier(change, canonical);
        await SendPendingChangeAsync(settings, resolved);
        return true;
    }

    private async Task<bool> HandleSupplierCorrectionTextAsync(TelegramBotSettings settings, string text)
    {
        var state = _operationsStateStore.Load();
        if (state.PendingSupplierCorrection is not { } id) return false;
        if (text.Trim() is "/cancel" or "չեղարկել")
        { state.PendingSupplierCorrection = null; _operationsStateStore.Save(state); await TelegramBotClient.SendMessageAsync(settings, "Անվան ուղղումը չեղարկվեց։"); return true; }
        var change = _pendingEmployeeOrderChangeStore.Load().FirstOrDefault(x => x.Id == id);
        if (change is null) { state.PendingSupplierCorrection = null; _operationsStateStore.Save(state); return false; }
        var canonical = ExactSupplier(text.Trim());
        if (canonical is null)
        {
            await TelegramBotClient.SendMessageAsync(settings, "Այդ անունը բազայում չի գտնվել։ Գրեք բազայի ճիշտ անունը կամ /cancel-ից հետո ընտրեք «Նոր մատակարար է»։");
            return true;
        }
        await SendPendingChangeAsync(settings, ResolveSupplier(change, canonical));
        return true;
    }

    private Task SendPendingChangeAsync(TelegramBotSettings settings, PendingEmployeeOrderChange change)
    {
        if (change.RequiresSupplierReview) return SendSupplierReviewAsync(settings, change);
        return TelegramBotClient.SendMessageAsync(settings,
            $"⏳ Հաստատում — {change.Date:dd.MM.yyyy}\n{change.Supplier}\nՊլան՝ {change.PlannedOrder:N0}/{change.PlannedPayment:N0}/{change.PlannedOldDebtPayment:N0}\nՓաստացի՝ {change.ActualOrder:N0}/{change.ActualPayment:N0}/{change.ActualOldDebtPayment:N0}\nԱշխատակից՝ {change.ReportedByName}",
            [ [new TelegramInlineButton("Հաստատել", $"emporderapprove:{change.Id}"), new TelegramInlineButton("Մերժել", $"emporderreject:{change.Id}")] ]);
    }

    private async void ResolveSupplierFromDesktop(Guid id)
    {
        try
        {
            var change = _pendingEmployeeOrderChangeStore.Load().FirstOrDefault(x => x.Id == id);
            if (change is null) { MessageBox.Show("Գրանցումն արդեն մշակված է։ Թարմացրեք ցանկը։"); return; }
            var window = new PendingOrderEditWindow(change, KnownSupplierNames()) { Owner = this };
            if (window.ShowDialog() != true || window.Result is not { } values) return;
            var canonical = ExactSupplier(values.Supplier);
            if (canonical is null && MessageBox.Show($"Այս անունը բազայում չկա՝ {values.Supplier}։ Պահպանե՞լ որպես նոր մատակարարի սպասող գրանցում։", "Նոր մատակարար", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            values = values with { Supplier = canonical ?? values.Supplier };
            var baseline = PlannedSuppliersFor(change.Date).FirstOrDefault(x => string.Equals(x.Supplier, values.Supplier, StringComparison.OrdinalIgnoreCase));
            var updated = PendingOrderEditing.Apply(change, values, baseline, DateTime.Now);
            var all = _pendingEmployeeOrderChangeStore.Load();
            var index = all.FindIndex(x => x.Id == id);
            if (index < 0 || !PendingOrderEditing.Unchanged(change, all[index]))
            { MessageBox.Show("Այս գրանցումն արդեն փոփոխվել կամ հաստատվել է։ Թարմացրեք ցանկը և կրկին բացեք այն։"); await LoadAsync("Approvals"); return; }
            all[index] = updated;
            _pendingEmployeeOrderChangeStore.Save(all);
            var state = _operationsStateStore.Load();
            if (state.PendingSupplierCorrection == id)
            { state.PendingSupplierCorrection = null; _operationsStateStore.Save(state); }
            await LoadAsync("Approvals");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Գրանցման խմբագրում"); }
    }
}
