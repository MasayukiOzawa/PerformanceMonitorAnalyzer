using System.Text.RegularExpressions;

namespace PerformanceMonitorAnalyzer;

/// <summary>
/// カウンターパス用のワイルドカードマッチャー。
/// </summary>
public static class CounterPathPatternMatcher
{
    public static bool MatchesPattern(string pattern, string counterPath)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(counterPath))
        {
            return false;
        }

        var normalizedPattern = pattern.Trim();
        var candidatePaths = GetCandidatePaths(counterPath).ToArray();

        if (candidatePaths.Any(candidatePath => WildcardEquals(normalizedPattern, candidatePath)))
        {
            return true;
        }

        if (!normalizedPattern.EndsWith(@"\*", StringComparison.Ordinal))
        {
            return false;
        }

        var objectPattern = normalizedPattern[..^2];
        return candidatePaths
            .Select(GetObjectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Any(objectPath => WildcardEquals(objectPattern, objectPath));
    }

    private static IEnumerable<string> GetCandidatePaths(string counterPath)
    {
        var normalizedPath = counterPath.Trim();
        yield return normalizedPath;

        var machineLessPath = RemoveMachineName(normalizedPath);
        if (!normalizedPath.Equals(machineLessPath, StringComparison.OrdinalIgnoreCase))
        {
            yield return machineLessPath;
        }
    }

    private static string RemoveMachineName(string counterPath)
    {
        if (!counterPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return counterPath;
        }

        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return counterPath;
        }

        return @"\" + string.Join(@"\", parts.Skip(1));
    }

    private static string GetObjectPath(string counterPath)
    {
        var lastSeparator = counterPath.LastIndexOf('\\');
        return lastSeparator > 0 ? counterPath[..lastSeparator] : counterPath;
    }

    private static bool WildcardEquals(string pattern, string candidatePath)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        return Regex.IsMatch(
            candidatePath,
            regexPattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
