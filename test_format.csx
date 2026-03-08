using System;

Console.WriteLine($"Testing F0 format with decimal values:");
Console.WriteLine($"1280.5 formatted as F0: {1280.5:F0}");
Console.WriteLine($"720.25 formatted as F0: {720.25:F0}");
Console.WriteLine($"1920.9 formatted as F0: {1920.9:F0}");
Console.WriteLine($"");
Console.WriteLine($"Note: F0 rounds to nearest integer (bankers rounding)");
