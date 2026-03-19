using SimNextgenApp.Observability.Internal;

namespace SimNextgenApp.Tests.Observability;

public class OTelMeterProviderTests
{
    [Fact(DisplayName = "CreateCounter should normalize names already prefixed with 'sna.' by keeping them as-is.")]
    public void CreateCounter_WithSnaPrefix_NormalizesCorrectly()
    {
        // Act
        var counter = OTelMeterProvider.CreateCounter<int>("sna.server.loads_completed");

        // Assert - Should keep as-is (no double prefix)
        Assert.NotNull(counter);
        Assert.Equal("sna.server.loads_completed", counter.Name);
    }

    [Fact(DisplayName = "CreateHistogram should normalize names already prefixed with 'sna.' by keeping them as-is.")]
    public void CreateHistogram_WithSnaPrefix_NormalizesCorrectly()
    {
        // Act
        var histogram = OTelMeterProvider.CreateHistogram<double>("sna.server.sojourn_time");

        // Assert - Should keep as-is (no double prefix)
        Assert.NotNull(histogram);
        Assert.Equal("sna.server.sojourn_time", histogram.Name);
    }

    [Fact(DisplayName = "CreateCounter should add 'sna.' prefix to raw names.")]
    public void CreateCounter_WithoutSnaPrefix_AddsPrefixAutomatically()
    {
        // Act
        var counter = OTelMeterProvider.CreateCounter<int>("server.loads_completed");

        // Assert
        Assert.NotNull(counter);
        Assert.Equal("sna.server.loads_completed", counter.Name);
    }

    [Fact(DisplayName = "CreateHistogram should add 'sna.' prefix to raw names.")]
    public void CreateHistogram_WithoutSnaPrefix_AddsPrefixAutomatically()
    {
        // Act
        var histogram = OTelMeterProvider.CreateHistogram<double>("server.sojourn_time", unit: "s");

        // Assert
        Assert.NotNull(histogram);
        Assert.Equal("sna.server.sojourn_time", histogram.Name);
    }

    [Fact(DisplayName = "CreateCounter should reject null or empty names.")]
    public void CreateCounter_NullOrEmptyName_ThrowsArgumentException()
    {
        // Act & Assert - Null
        var ex1 = Assert.Throws<ArgumentException>(() =>
        {
            OTelMeterProvider.CreateCounter<int>(null!);
        });
        Assert.Contains("cannot be null or empty", ex1.Message);

        // Empty
        var ex2 = Assert.Throws<ArgumentException>(() =>
        {
            OTelMeterProvider.CreateCounter<int>("");
        });
        Assert.Contains("cannot be null or empty", ex2.Message);

        // Whitespace
        var ex3 = Assert.Throws<ArgumentException>(() =>
        {
            OTelMeterProvider.CreateCounter<int>("   ");
        });
        Assert.Contains("cannot be null or empty", ex3.Message);
    }

    [Fact(DisplayName = "CreateHistogram should reject null or empty names.")]
    public void CreateHistogram_NullOrEmptyName_ThrowsArgumentException()
    {
        // Act & Assert - Null
        var ex1 = Assert.Throws<ArgumentException>(() =>
        {
            OTelMeterProvider.CreateHistogram<double>(null!);
        });
        Assert.Contains("cannot be null or empty", ex1.Message);

        // Empty
        var ex2 = Assert.Throws<ArgumentException>(() =>
        {
            OTelMeterProvider.CreateHistogram<double>("");
        });
        Assert.Contains("cannot be null or empty", ex2.Message);
    }

    [Fact(DisplayName = "Normalization should be case-insensitive for 'sna.' prefix.")]
    public void CreateCounter_CaseInsensitivePrefixNormalization_PreservesOriginal()
    {
        // Act - Upper case prefix
        var counter1 = OTelMeterProvider.CreateCounter<int>("SNA.server.loads");

        // Assert - Case-insensitive match, keeps original
        Assert.Equal("SNA.server.loads", counter1.Name);

        // Act - Mixed case prefix
        var counter2 = OTelMeterProvider.CreateCounter<int>("SnA.server.loads");

        // Assert - Case-insensitive match, keeps original
        Assert.Equal("SnA.server.loads", counter2.Name);
    }

    [Fact(DisplayName = "CreateCounter with unit and description should work correctly.")]
    public void CreateCounter_WithUnitAndDescription_CreatesCorrectly()
    {
        // Act
        var counter = OTelMeterProvider.CreateCounter<long>(
            "server.requests",
            unit: "requests",
            description: "Total HTTP requests");

        // Assert
        Assert.NotNull(counter);
        Assert.Equal("sna.server.requests", counter.Name);
        Assert.Equal("requests", counter.Unit);
        Assert.Equal("Total HTTP requests", counter.Description);
    }

    [Fact(DisplayName = "CreateHistogram with unit and description should work correctly.")]
    public void CreateHistogram_WithUnitAndDescription_CreatesCorrectly()
    {
        // Act
        var histogram = OTelMeterProvider.CreateHistogram<double>(
            "server.duration",
            unit: "ms",
            description: "Request duration");

        // Assert
        Assert.NotNull(histogram);
        Assert.Equal("sna.server.duration", histogram.Name);
        Assert.Equal("ms", histogram.Unit);
        Assert.Equal("Request duration", histogram.Description);
    }
}
