using System;

namespace PerformanceMonitorAnalyzer
{
    /// <summary>
    /// サイズ設定ダイアログで共通利用する入力解析ヘルパー
    /// </summary>
    internal static class DialogSizeInputHelper
    {
        internal enum NumericMode
        {
            IntegerOnly,
            DecimalAllowed
        }

        internal enum ValidationState
        {
            InvalidFormat,
            OutOfRange,
            Valid,
            Auto
        }

        internal sealed class ValidationOptions
        {
            public ValidationOptions(
                double minWidth,
                double maxWidth,
                double minHeight,
                double maxHeight,
                NumericMode numericMode,
                bool allowAuto = false,
                string autoValue = "自動")
            {
                MinWidth = minWidth;
                MaxWidth = maxWidth;
                MinHeight = minHeight;
                MaxHeight = maxHeight;
                NumericMode = numericMode;
                AllowAuto = allowAuto;
                AutoValue = autoValue;
            }

            public double MinWidth { get; }

            public double MaxWidth { get; }

            public double MinHeight { get; }

            public double MaxHeight { get; }

            public NumericMode NumericMode { get; }

            public bool AllowAuto { get; }

            public string AutoValue { get; }
        }

        internal sealed class ValidationResult
        {
            public ValidationResult(ValidationState state, double width = 0, double height = 0)
            {
                State = state;
                Width = width;
                Height = height;
            }

            public ValidationState State { get; }

            public double Width { get; }

            public double Height { get; }

            public bool IsAuto => State == ValidationState.Auto;
        }

        public static ValidationResult Evaluate(string? widthText, string? heightText, ValidationOptions options)
        {
            if (options.AllowAuto &&
                string.Equals(widthText, options.AutoValue, StringComparison.Ordinal) &&
                string.Equals(heightText, options.AutoValue, StringComparison.Ordinal))
            {
                return new ValidationResult(ValidationState.Auto);
            }

            if (!TryParseDimension(widthText, options.NumericMode, out double width) ||
                !TryParseDimension(heightText, options.NumericMode, out double height))
            {
                return new ValidationResult(ValidationState.InvalidFormat);
            }

            bool isInRange = width >= options.MinWidth &&
                             width <= options.MaxWidth &&
                             height >= options.MinHeight &&
                             height <= options.MaxHeight;

            return new ValidationResult(
                isInRange ? ValidationState.Valid : ValidationState.OutOfRange,
                width,
                height);
        }

        public static bool TryParsePreset(
            string? presetValue,
            NumericMode numericMode,
            out string widthText,
            out string heightText)
        {
            widthText = string.Empty;
            heightText = string.Empty;

            if (string.IsNullOrWhiteSpace(presetValue))
            {
                return false;
            }

            var values = presetValue.Split(',', StringSplitOptions.TrimEntries);
            if (values.Length != 2 ||
                !TryParseDimension(values[0], numericMode, out _) ||
                !TryParseDimension(values[1], numericMode, out _))
            {
                return false;
            }

            widthText = values[0];
            heightText = values[1];
            return true;
        }

        private static bool TryParseDimension(string? valueText, NumericMode numericMode, out double value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(valueText))
            {
                return false;
            }

            if (numericMode == NumericMode.IntegerOnly)
            {
                if (!int.TryParse(valueText, out int intValue))
                {
                    return false;
                }

                value = intValue;
                return true;
            }

            return double.TryParse(valueText, out value);
        }
    }
}
