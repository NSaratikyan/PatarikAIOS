using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

public sealed record SupplierWeekPlanRow(DateOnly Date, string Supplier, decimal OrderAmount, decimal PaymentAmount, decimal OldDebtPayment, decimal Debt, bool HasActualDebt = false);

public sealed record RecurringSupplierDayRule(string Supplier, DayOfWeek PreviousDay, DayOfWeek NewDay, DateOnly EffectiveFrom, string Note);
public sealed record SupplierDateScheduleOverride(DateOnly Date, string Supplier, bool IsIncluded, string Note);
public sealed record RecurringSupplierMembershipRule(string Supplier, DayOfWeek Day, bool IsIncluded, DateOnly EffectiveFrom, string Note);

public static class SupplierWeekPlanSeed
{
    public static List<SupplierWeekPlanRow> Create()
    {
        var result = new List<SupplierWeekPlanRow>();
        // 20.07.2026 — Երկուշաբթի
        Add(result,20,"Աթենք Կիսաֆաբրիկատ","Աթենք","Բիոկաթ","Մասիս Պանիր","Նատֆուդ","Ջերմուկ Գրուպ","Ջին","Ռոյալ Արմենիա","Սարատիկյան Նարեկ","Սևանի Պանիր","Վինսթոն-Վինկո","Ֆիլիպ Մորրիս");
        // 21.07.2026 — Երեքշաբթի
        Add(result,21,"Ալ-Տա Գրուպ","Ալեքս Գրուպ Մոյա Սեմյա","Ակվա Սիթի","Ավաս","Արո Ջեմ","Բիզնես Փարթնըր","Բոյարսկայա-Դրոժ","Գանա Գրուպ Պլյուս","Դուետ Քոմփանի","Դուստր Մարիաննա","Լոշիկ","Կոկա-Կոլա","Հին Ապարան Սամո","Ձու Մասիս","Մենթոս","Նեվիս","Սիներգիա","Վիտամինա","Փինկ Բերի");
        // 22.07.2026 — Չորեքշաբթի
        Add(result,22,"Աթենք","Ալեքս Էնդ Հոլդինգ՝ ձեթ/շաքար","Ալյուր-Մխո","Ապաչի Գրուպ","Գրանդ Տոբակո","Դավիդով-Ավերս","Լումա","Կիլիկիա","Հեդ Քորփորեյշն","Սնեկարմ");
        // 23.07.2026 — Հինգշաբթի
        Add(result,23,"Ալյուր-Մխո","Ապաչի Գրուպ","Առգե Բիզնես","Բոյարսկայա-Դրոժ","Դուստր Մարիաննա","Էլիտ Դելյուքս","Մասիս Պանիր","Մենթոս","Ջին","Սևանի Պանիր","Վինսթոն-Վինկո","Ֆիլիպ Մորրիս");
        // 24.07.2026 — Ուրբաթ
        Add(result,24,"Ալեքս Էնդ Հոլդինգ՝ բրինձ","Ապարան Ջուր","Արմբիըր","Արմտորգ","Արո Ջեմ","Դարոյնք","Դուետ Քոմփանի","Լոշիկ","Կիլիկիա","Կոկա-Կոլա","Հին Ապարան Սամո","Նատֆուդ","Նեվիս","Չինար Սառույց");
        // 25.07.2026 — Շաբաթ
        Add(result,25,"Աթենք","Բիոկաթ","Գուդջոբ-Ստարտ","Գրանդ Տոբակո","Դավիդով-Ավերս","Դուստր Մարիաննա","Կոլաստիկ","Ձու Մասիս","Մարտին Սթար","Սևանի Պանիր");
        // 26.07.2026 — Կիրակի
        Add(result,26,"Չինար");
        return result;
    }

    private static void Add(List<SupplierWeekPlanRow> rows, int day, params string[] suppliers) => rows.AddRange(suppliers.Select(s => new SupplierWeekPlanRow(new DateOnly(2026, 7, day), s, 0m, 0m, 0m, 0m)));

    // The supplied 20–26 July list is the weekly template. It is generated
    // for every selected date, rather than being limited to one sample week.
    public static List<SupplierWeekPlanRow> ForDate(DateOnly date) =>
        Create().Where(x => x.Date.DayOfWeek == date.DayOfWeek)
            .Select(x => x with { Date = date }).ToList();

    public static IReadOnlyList<string> AllSuppliers() =>
        Create().Select(x => x.Supplier).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
}

public sealed class LocalSupplierWeekPlanStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "supplier-week-2026-07-20.json");
    private readonly string _legacyPath = Path.Combine(AppContext.BaseDirectory, "data", "supplier-week-2026-07-20.json");
    public List<SupplierWeekPlanRow> LoadOrSeed()
    {
        try
        {
            if (File.Exists(_path)) return JsonSerializer.Deserialize<List<SupplierWeekPlanRow>>(File.ReadAllText(_path)) ?? SupplierWeekPlanSeed.Create();
            // Move previous versions' data once, so existing work is not lost after this upgrade.
            if (File.Exists(_legacyPath))
            {
                var migrated = JsonSerializer.Deserialize<List<SupplierWeekPlanRow>>(File.ReadAllText(_legacyPath)) ?? SupplierWeekPlanSeed.Create();
                Save(migrated);
                return migrated;
            }
        }
        catch (JsonException) { }
        var seed = SupplierWeekPlanSeed.Create(); Save(seed); return seed;
    }
    public void Save(IEnumerable<SupplierWeekPlanRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class LocalRecurringSupplierDayRuleStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "data", "recurring-supplier-day-rules.json");
    public List<RecurringSupplierDayRule> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<RecurringSupplierDayRule>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<RecurringSupplierDayRule> rules)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class LocalSupplierScheduleOverrideStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "data", "supplier-schedule-overrides.json");
    public List<SupplierDateScheduleOverride> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<SupplierDateScheduleOverride>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<SupplierDateScheduleOverride> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class LocalRecurringSupplierMembershipRuleStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "data", "recurring-supplier-membership-rules.json");
    public List<RecurringSupplierMembershipRule> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<RecurringSupplierMembershipRule>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<RecurringSupplierMembershipRule> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
