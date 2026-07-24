namespace PatarikAIOS;

public static class SalaryEmployeeMatcher
{
    /// <summary>Returns a known employee when the difference is only a small typo.</summary>
    public static string? FindKnown(string entered, IEnumerable<string> knownEmployees)
    {
        var candidates = knownEmployees.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var normalized = Normalize(entered);
        var exact = candidates.FirstOrDefault(x => Normalize(x) == normalized);
        if (exact is not null) return exact;
        var best = candidates
            .Select(x => new { Name = x, Distance = Distance(normalized, Normalize(x)) })
            .OrderBy(x => x.Distance).ThenBy(x => x.Name.Length).FirstOrDefault();
        if (best is null) return null;
        var maximum = normalized.Length <= 5 ? 1 : 2;
        return best.Distance <= maximum ? best.Name : null;
    }

    private static string Normalize(string text) => new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    private static int Distance(string a, string b)
    {
        var matrix = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) matrix[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) matrix[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
            matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1), matrix[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return matrix[a.Length, b.Length];
    }
}
