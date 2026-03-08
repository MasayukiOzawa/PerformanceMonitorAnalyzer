using System;
using PerformanceMonitorAnalyzer;

// Test some edge cases around floating point comparison
var testValues = new[] { 1.0, 0.1, 0.01, 0.001, 0.000000001, 0.9999999999, 0.1000000001 };

foreach (var val in testValues)
{
    var label = ScaleCatalog.GetLabel(val);
    Console.WriteLine($"Value: {val:E15} -> Label: {label}");
}
