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
    List<string> NonMovablePaymentCategories)
{
    public static CashFlowPolicy Default => new(
        MinimumReserve: 25_000m,
        NonMovableSuppliers:
        [
            "Տոբակ", "Նեվիս", "Վինկո", "Ֆիլիպ Մորիս",
            "Մարիաննա", "Ալյուր"
        ],
        NonMovablePaymentCategories: ["Կոմունալ", "Վարձավճար"]);
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
                        NonMovablePaymentCategories = policy.NonMovablePaymentCategories ?? []
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
