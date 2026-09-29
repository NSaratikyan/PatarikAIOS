using System.IO;
using System.Text.Json;

namespace PatarikAIOS;

/// <summary>
/// Owner-defined cash-flow rules.  These are deliberately stored separately
/// from daily plans so a policy stays in force after the application restarts.
/// </summary>
public sealed record CashFlowPolicy(
    decimal MinimumReserve,
    List<string> NonMovableSuppliers,
    List<string> NonMovablePaymentCategories,
    decimal? WeeklySalesBaselineOverride = null,
    FixedCostAllocationMode FixedCostAllocation = FixedCostAllocationMode.ByDueDate)
{
    public static CashFlowPolicy Default => new(
        MinimumReserve: 25_000m,
        NonMovableSuppliers:
        [
            "Տոբակ", "Նեվիս", "Վինկո", "Ֆիլիպ Մորիս",
            "Մարիաննա", "Ալյուր"
        ],
        NonMovablePaymentCategories: ["Կոմունալ", "Վարձավճար"],
        WeeklySalesBaselineOverride: null,
        FixedCostAllocation: FixedCostAllocationMode.ByDueDate);
}

/// <summary>
/// This affects the planning reserve only.  Actual cash is still deducted on
/// the real payment day, so a reserve is never recorded as a second payment.
/// </summary>
public enum FixedCostAllocationMode
{
    ByDueDate,
    EvenlyAcrossMonth
}

public sealed class CashFlowPolicyStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "cash-flow-policy.json");

    public CashFlowPolicy LoadOrCreate()
    {
        try
        {
            if (File.Exists(_path))
            {
                var policy = JsonSerializer.Deserialize<CashFlowPolicy>(File.ReadAllText(_path), Options);
                if (policy is not null)
                    return policy with
                    {
                        NonMovableSuppliers = policy.NonMovableSuppliers ?? [],
                        NonMovablePaymentCategories = policy.NonMovablePaymentCategories ?? [],
                        FixedCostAllocation = policy.FixedCostAllocation
                    };
            }
        }
        catch
        {
            // A malformed local settings file must not stop business reporting.
        }

        var initial = CashFlowPolicy.Default;
        Save(initial);
        return initial;
    }

    public void Save(CashFlowPolicy policy)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(policy, Options));
    }
}
