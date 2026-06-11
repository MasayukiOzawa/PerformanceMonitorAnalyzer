namespace PerformanceMonitorAnalyzer;

public static class CounterPathFormatter
{
    public static string GetDisplayName(string counterPath)
    {
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            var objectName = parts[1];
            var counterName = parts[2];

            if (objectName.Contains('(') && objectName.Contains(')'))
            {
                var startIndex = objectName.IndexOf('(');
                var endIndex = objectName.IndexOf(')');
                var instanceName = objectName.Substring(startIndex + 1, endIndex - startIndex - 1);
                objectName = objectName[..startIndex];

                return $"{objectName}({instanceName}) - {counterName}";
            }

            return $"{objectName} - {counterName}";
        }
        else if (parts.Length >= 2)
        {
            var objectName = parts[0];
            var counterName = parts[1];

            if (objectName.Contains('(') && objectName.Contains(')'))
            {
                var startIndex = objectName.IndexOf('(');
                var endIndex = objectName.IndexOf(')');
                var instanceName = objectName.Substring(startIndex + 1, endIndex - startIndex - 1);
                objectName = objectName[..startIndex];

                return $"{objectName}({instanceName}) - {counterName}";
            }

            return $"{objectName} - {counterName}";
        }

        return counterPath;
    }

    public static string GetComputerName(string counterPath, string? actualComputerName)
    {
        var parts = counterPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        if (counterPath.StartsWith("\\\\", StringComparison.Ordinal) && parts.Length >= 3)
        {
            return parts[0];
        }

        if (!string.IsNullOrEmpty(actualComputerName))
        {
            return actualComputerName;
        }

        return "ローカルコンピューター";
    }

    public static string Normalize(string path)
    {
        return path.Replace("\\", "/").Replace("\"", "").Trim().ToLowerInvariant();
    }

    public static string ExtractCounterDisplayName(string fullPath)
    {
        var parts = fullPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? parts[2] :
               parts.Length >= 2 ? parts[1] :
               fullPath;
    }

    public static string ExtractObjectDisplayName(string fullPath)
    {
        var parts = fullPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var objectPart = parts.Length >= 3 ? parts[1] :
                         parts.Length >= 2 ? parts[0] :
                         string.Empty;
        var startIndex = objectPart.IndexOf('(');
        return startIndex > 0 ? objectPart[..startIndex] : objectPart;
    }

    public static string ExtractInstanceDisplayName(string fullPath)
    {
        var parts = fullPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var objectPart = parts.Length >= 3 ? parts[1] :
                         parts.Length >= 2 ? parts[0] :
                         string.Empty;
        var startIndex = objectPart.IndexOf('(');
        var endIndex = objectPart.LastIndexOf(')');
        return startIndex >= 0 && endIndex > startIndex
            ? objectPart.Substring(startIndex + 1, endIndex - startIndex - 1)
            : string.Empty;
    }
}
