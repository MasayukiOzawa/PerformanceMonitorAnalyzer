using System.Globalization;

namespace PerformanceMonitorAnalyzer;

public readonly record struct ScaleOption(double Value, string Label);

public static class ScaleCatalog
{
    private static readonly ScaleOption[] SupportedScaleOptions =
    {
        new(1000000000.0, "1000000000"),
        new(100000000.0, "100000000"),
        new(10000000.0, "10000000"),
        new(1000000.0, "1000000"),
        new(100000.0, "100000"),
        new(10000.0, "10000"),
        new(1000.0, "1000"),
        new(100.0, "100"),
        new(10.0, "10"),
        new(1.0, "1.0"),
        new(0.1, "0.1"),
        new(0.01, "0.01"),
        new(0.001, "0.001"),
        new(0.0001, "0.0001"),
        new(0.00001, "0.00001"),
        new(0.000001, "0.000001"),
        new(0.0000001, "0.0000001"),
        new(0.00000001, "0.00000001"),
        new(0.000000001, "0.000000001"),
    };

    public static IReadOnlyList<ScaleOption> SupportedOptions => SupportedScaleOptions;

    public static IReadOnlyList<double> SupportedValues { get; } =
        Array.AsReadOnly(SupportedScaleOptions.Select(option => option.Value).ToArray());

    public static IReadOnlyList<string> SupportedLabels { get; } =
        Array.AsReadOnly(SupportedScaleOptions.Select(option => option.Label).ToArray());

    public static string GetLabel(double scale)
    {
        foreach (var option in SupportedScaleOptions)
        {
            if (Math.Abs(option.Value - scale) < 1e-12)
            {
                return option.Label;
            }
        }

        var decimalLabel = scale.ToString("0.###############", CultureInfo.InvariantCulture);
        return decimalLabel == "0" && scale != 0
            ? scale.ToString("0.###############E+0", CultureInfo.InvariantCulture)
            : decimalLabel;
    }

    public static bool TryCalculateScaleToTarget(double maximumAbsoluteValue, out double scale, double targetValue = 100.0)
    {
        scale = 0;

        if (!double.IsFinite(maximumAbsoluteValue) ||
            !double.IsFinite(targetValue) ||
            maximumAbsoluteValue <= 0 ||
            targetValue <= 0)
        {
            return false;
        }

        scale = RoundToNiceScale(targetValue / maximumAbsoluteValue);
        return double.IsFinite(scale) && scale > 0;
    }

    public static double RoundToNiceScale(double scale)
    {
        if (!double.IsFinite(scale) || scale <= 0)
        {
            return 0;
        }

        var exponent = Math.Floor(Math.Log10(scale));
        var magnitude = Math.Pow(10, exponent);
        var normalized = scale / magnitude;
        var niceNormalized = normalized switch
        {
            < 1.5 => 1.0,
            < 3.5 => 2.0,
            < 7.5 => 5.0,
            _ => 10.0
        };

        return niceNormalized * magnitude;
    }
}
