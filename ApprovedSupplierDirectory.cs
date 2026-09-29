using System.Text.Json;
using System.Text.RegularExpressions;

namespace PatarikAIOS;

/// <summary>Presentation/selection catalog only. Never migrates financial records.</summary>
public static class ApprovedSupplierDirectory
{
    private static readonly Dictionary<string,string> Names = Load();
    private static string Key(string name) => new(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    private static Dictionary<string,string> Load()
    {
        using var stream = typeof(ApprovedSupplierDirectory).Assembly.GetManifestResourceStream("PatarikAIOS.approved-supplier-names.json")
            ?? throw new InvalidOperationException("Հաստատված մատակարարների ցանկը չի գտնվել։");
        using var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string,string>();
        foreach (var group in doc.RootElement.GetProperty("groups").EnumerateArray())
        {
            var canonical = group.GetProperty("name").GetString()!;
            result[Key(canonical)] = canonical;
            foreach (var alias in group.GetProperty("aliases").EnumerateArray()) result[Key(alias.GetString()!)] = canonical;
        }
        foreach (var group in doc.RootElement.GetProperty("keep_separate").EnumerateArray())
            foreach (var entry in group.EnumerateArray())
            {
                var name = entry.GetString()!;
                result.TryAdd(Key(name),name);
            }
        result[Key("ՏՆՏԵՍԱԿԱՆ")] = "ՏՆՏԵՍԱԿԱՆ";
        return result;
    }
    public static IReadOnlyList<string> All => Names.Values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToList();
    public static IReadOnlyList<string> ForNames(IEnumerable<string> existing) => existing
        .Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>Canonical(x) ?? x)
        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToList();
    public static string? Canonical(string input)
    {
        var stripped = Regex.Replace(input, @"\d{2}[․./]\d{2}[․./]\d{4}", "").Trim(' ','-','–');
        return Names.GetValueOrDefault(Key(stripped));
    }
    public static UIElement View(UIElement historicalDailyView, IEnumerable<string> existing)
    {
        var visible = ForNames(existing);
        var list = new StackPanel { Margin = new Thickness(12) };
        list.Children.Add(new TextBlock { Text = $"Մատակարարներ · {visible.Count}", FontSize = 20, FontWeight = FontWeights.Bold });
        list.Children.Add(new TextBlock { Text = "Քննարկված տարբերակները ցուցադրվում են համաձայնեցված անունով։ Մնացած անուններն անփոփոխ են։ Նախկին պատվերներն ու գումարները պահպանված են «Օրվա գրանցումներ» ներդիրում։ Այս ցանկը չի միավորում պարտքերը։", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,12) });
        var search = new TextBox { Margin = new Thickness(0,0,0,10), ToolTip = "Փնտրեք անունով կամ նախկին տարբերակով" };
        list.Children.Add(search);
        var names = new StackPanel();
        void Refresh()
        {
            names.Children.Clear();
            var canonical = Canonical(search.Text);
            foreach (var name in visible.Where(n=>string.IsNullOrWhiteSpace(search.Text) ||
                n.Contains(search.Text,StringComparison.OrdinalIgnoreCase) || n==canonical))
                names.Children.Add(new TextBlock { Text = name, Margin = new Thickness(4,6,4,6), FontSize = 15 });
        }
        search.TextChanged += (_,_)=>Refresh(); Refresh(); list.Children.Add(names);
        return new TabControl { Items =
        {
            new TabItem { Header = "Մատակարարների ցանկ", Content = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } },
            new TabItem { Header = "Օրվա գրանցումներ / նախկին տվյալներ", Content = historicalDailyView }
        }};
    }
}
