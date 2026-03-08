using PerformanceMonitorAnalyzer;

namespace PerformanceMonitorAnalyzer.Tests;

public class DialogSizeInputHelperTests
{
    private static readonly DialogSizeInputHelper.ValidationOptions GraphDialogOptions =
        new(200, 3840, 150, 2160, DialogSizeInputHelper.NumericMode.IntegerOnly, allowAuto: true);

    private static readonly DialogSizeInputHelper.ValidationOptions WindowDialogOptions =
        new(400, 3840, 300, 2160, DialogSizeInputHelper.NumericMode.DecimalAllowed);

    [Fact]
    public void Evaluate_ReturnsAuto_WhenGraphDialogUsesAutoValue()
    {
        var result = DialogSizeInputHelper.Evaluate("自動", "自動", GraphDialogOptions);

        Assert.Equal(DialogSizeInputHelper.ValidationState.Auto, result.State);
        Assert.True(result.IsAuto);
    }

    [Fact]
    public void Evaluate_RejectsDecimalInput_WhenIntegerOnlyModeIsUsed()
    {
        var result = DialogSizeInputHelper.Evaluate("800.5", "400", GraphDialogOptions);

        Assert.Equal(DialogSizeInputHelper.ValidationState.InvalidFormat, result.State);
    }

    [Fact]
    public void Evaluate_AllowsDecimalInputWithinRange_WhenWindowDialogUsesNormalState()
    {
        var result = DialogSizeInputHelper.Evaluate("1280.5", "720.25", WindowDialogOptions);

        Assert.Equal(DialogSizeInputHelper.ValidationState.Valid, result.State);
        Assert.Equal(1280.5, result.Width);
        Assert.Equal(720.25, result.Height);
    }

    [Fact]
    public void Evaluate_ReturnsOutOfRange_WhenNumericInputExceedsBounds()
    {
        var result = DialogSizeInputHelper.Evaluate("399", "500", WindowDialogOptions);

        Assert.Equal(DialogSizeInputHelper.ValidationState.OutOfRange, result.State);
        Assert.Equal(399, result.Width);
        Assert.Equal(500, result.Height);
    }

    [Fact]
    public void Evaluate_RejectsAutoValue_WhenAllowAutoIsFalse()
    {
        var result = DialogSizeInputHelper.Evaluate("自動", "自動", WindowDialogOptions);

        Assert.Equal(DialogSizeInputHelper.ValidationState.InvalidFormat, result.State);
    }

    [Fact]
    public void Evaluate_AcceptsBoundaryValues_WhenWithinConfiguredRange()
    {
        var graphResult = DialogSizeInputHelper.Evaluate("200", "150", GraphDialogOptions);
        var windowResult = DialogSizeInputHelper.Evaluate("3840", "2160", WindowDialogOptions);

        Assert.Equal(DialogSizeInputHelper.ValidationState.Valid, graphResult.State);
        Assert.Equal(DialogSizeInputHelper.ValidationState.Valid, windowResult.State);
    }

    [Fact]
    public void TryParsePreset_ValidatesPresetUsingRequestedNumericMode()
    {
        var parsed = DialogSizeInputHelper.TryParsePreset(
            "1024,768",
            DialogSizeInputHelper.NumericMode.IntegerOnly,
            out var widthText,
            out var heightText);

        var rejected = DialogSizeInputHelper.TryParsePreset(
            "1024.5,768",
            DialogSizeInputHelper.NumericMode.IntegerOnly,
            out _,
            out _);

        Assert.True(parsed);
        Assert.Equal("1024", widthText);
        Assert.Equal("768", heightText);
        Assert.False(rejected);
    }
}
