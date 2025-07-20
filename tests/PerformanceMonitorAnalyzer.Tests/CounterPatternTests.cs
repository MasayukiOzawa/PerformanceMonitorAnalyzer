using FluentAssertions;

namespace PerformanceMonitorAnalyzer.Tests;

public class CounterPatternTests
{
    [Fact]
    public void CounterPattern_ShouldHaveValidProperties()
    {
        // Arrange & Act
        var pattern = new CounterPattern
        {
            Name = "Test Pattern",
            Description = "Test Description",
            Counters = new List<string> { "\\Processor(_Total)\\% Processor Time" },
            Scale = 1.0
        };

        // Assert
        pattern.Name.Should().Be("Test Pattern");
        pattern.Description.Should().Be("Test Description");
        pattern.Counters.Should().ContainSingle("\\Processor(_Total)\\% Processor Time");
        pattern.Scale.Should().Be(1.0);
    }

    [Fact]
    public void CounterPattern_DefaultConstructor_ShouldInitializeCollections()
    {
        // Arrange & Act
        var pattern = new CounterPattern();

        // Assert
        pattern.Counters.Should().NotBeNull();
        pattern.Counters.Should().BeEmpty();
    }
}