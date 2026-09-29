using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

/// <summary>One manager-entered, factual daily salary amount. No salary is invented by the system.</summary>
public sealed record SalaryAccrual(Guid Id, DateOnly Date, string Employee, decimal Amount, string Note, DateTime CreatedAt);

/// <summary>A salary payment made to an employee for a specified work week.</summary>
public sealed record SalaryPayment(Guid Id, DateOnly WeekStart, string Employee, decimal Amount, DateOnly PaidDate, string Note, string? CashSource = null);

public sealed class SalaryStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _directory = Path.Combine(AppContext.BaseDirectory, "data");
    private string AccrualPath => Path.Combine(_directory, "salary-accruals.json");
    private string PaymentPath => Path.Combine(_directory, "salary-payments.json");

    public List<SalaryAccrual> LoadAccruals() => Load<SalaryAccrual>(AccrualPath);
    public List<SalaryPayment> LoadPayments() => Load<SalaryPayment>(PaymentPath);
    public void SaveAccruals(IEnumerable<SalaryAccrual> rows) => Save(AccrualPath, rows);
    public void SavePayments(IEnumerable<SalaryPayment> rows) => Save(PaymentPath, rows);
    public void SaveCorrection(IEnumerable<SalaryAccrual> accruals, IEnumerable<SalaryPayment> payments)
    {
        var oldAccruals=LoadAccruals(); var oldPayments=LoadPayments();
        var archive=Path.Combine(_directory,"salary-corrections",DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff")+".json");
        AtomicJsonFile.Save(archive,new { At=DateTime.Now, Accruals=oldAccruals, Payments=oldPayments });
        try { AtomicJsonFile.Save(AccrualPath,accruals.ToList()); AtomicJsonFile.Save(PaymentPath,payments.ToList()); }
        catch { AtomicJsonFile.Save(AccrualPath,oldAccruals); AtomicJsonFile.Save(PaymentPath,oldPayments); throw; }
    }

    private static List<T> Load<T>(string path)
    {
        try
        {
            if (File.Exists(path)) return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), Options) ?? [];
        }
        catch (JsonException) { }
        return [];
    }

    private void Save<T>(string path, IEnumerable<T> rows)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, JsonSerializer.Serialize(rows, Options));
    }
}

public static class SalaryRules
{
    public static DateOnly WeekStart(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysFromMonday);
    }

    public static decimal AccruedForWeek(IEnumerable<SalaryAccrual> accruals, DateOnly weekStart, string? employee = null) =>
        accruals.Where(x => x.Date >= weekStart && x.Date <= weekStart.AddDays(6) &&
                            (employee is null || string.Equals(x.Employee, employee, StringComparison.OrdinalIgnoreCase)))
                .Sum(x => x.Amount);

    public static decimal PaidForWeek(IEnumerable<SalaryPayment> payments, DateOnly weekStart, string? employee = null) =>
        payments.Where(x => x.WeekStart == weekStart &&
                            (employee is null || string.Equals(x.Employee, employee, StringComparison.OrdinalIgnoreCase)))
                .Sum(x => x.Amount);

    public static decimal BalanceForWeek(IEnumerable<SalaryAccrual> accruals, IEnumerable<SalaryPayment> payments, DateOnly weekStart, string? employee = null) =>
        AccruedForWeek(accruals, weekStart, employee) - PaidForWeek(payments, weekStart, employee);

    /// <summary>
    /// Payroll is due each Sunday. For future weeks without entered daily facts,
    /// the most recently completed week is used only as a forecast, never as an
    /// actual accrued salary.
    /// </summary>
    public static decimal PlannedSundayPayroll(IEnumerable<SalaryAccrual> accruals, IEnumerable<SalaryPayment> payments, DateOnly date)
    {
        if (date.DayOfWeek != DayOfWeek.Sunday) return 0m;
        var weekStart = WeekStart(date);
        var actual = BalanceForWeek(accruals, payments, weekStart);
        if (actual > 0m) return actual;
        var previousWeek = weekStart.AddDays(-7);
        return Math.Max(0m, BalanceForWeek(accruals, payments, previousWeek));
    }
}
