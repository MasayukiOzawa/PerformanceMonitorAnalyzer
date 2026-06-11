using System.Text.RegularExpressions;

namespace PerformanceMonitorAnalyzer;

public static class ValueFormatHelper
{
    public static string EstimateUnit(string counter)
    {
        var lowerCounter = counter.ToLower();

        if (lowerCounter.Contains("% processor time") || lowerCounter.Contains("% idle time"))
        {
            return "%";
        }

        if (lowerCounter.Contains("available mbytes") || lowerCounter.Contains("mbytes"))
        {
            return "MB";
        }

        if (lowerCounter.Contains("bytes") && !lowerCounter.Contains("mbytes"))
        {
            return "Bytes";
        }

        if (lowerCounter.Contains("/sec"))
        {
            return "/sec";
        }

        if (lowerCounter.Contains("count"))
        {
            return "count";
        }

        return "";
    }

    public static string FormatValueWithUnit(double value, string unit)
    {
        if (unit == "%")
        {
            return $"{value:N1}%";
        }

        if (unit == "MB")
        {
            return $"{value:N0} MB";
        }

        if (unit == "Bytes")
        {
            if (value >= 1073741824)
            {
                return $"{value / 1073741824:N2} GB";
            }

            if (value >= 1048576)
            {
                return $"{value / 1048576:N2} MB";
            }

            if (value >= 1024)
            {
                return $"{value / 1024:N2} KB";
            }

            return $"{value:N0} Bytes";
        }

        if (unit == "/sec")
        {
            return $"{value:N2}/sec";
        }

        if (unit == "count")
        {
            return $"{value:N0}";
        }

        return $"{value:N2}";
    }

    public static string ExtractUnit(string formattedValue)
    {
        var match = Regex.Match(formattedValue, @"[^\d\.,\-\s]+");
        return match.Success ? match.Value : "";
    }

    public static string FormatSamplingInterval(TimeSpan interval)
    {
        if (interval.TotalHours >= 1)
        {
            return $"{interval.TotalHours:F1}時間";
        }

        if (interval.TotalMinutes >= 1)
        {
            return $"{interval.TotalMinutes:F1}分";
        }

        if (interval.TotalSeconds >= 1)
        {
            return $"{interval.TotalSeconds:F1}秒";
        }

        if (interval.TotalMilliseconds >= 1)
        {
            return $"{interval.TotalMilliseconds:F0}ミリ秒";
        }

        return "不明";
    }

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        if (bytes < 1024 * 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    public static string FormatCounterValue(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "-";
        }

        if (Math.Abs(value) >= 1000000)
        {
            return $"{value / 1000000:F2}M";
        }

        if (Math.Abs(value) >= 1000)
        {
            return $"{value / 1000:F2}K";
        }

        if (Math.Abs(value) >= 1)
        {
            return $"{value:F2}";
        }

        return $"{value:F4}";
    }
}
