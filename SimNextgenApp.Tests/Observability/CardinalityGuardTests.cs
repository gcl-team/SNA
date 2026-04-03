using SimNextgenApp.Observability.Advanced;

namespace SimNextgenApp.Tests.Observability;

public class CardinalityGuardTests
{
    [Fact]
    public void Constructor_WithValidThreshold_CreatesInstance()
    {
        var guard = new CardinalityGuard(threshold: 100);
        Assert.NotNull(guard);
    }

    [Fact]
    public void Constructor_WithDefaultThreshold_CreatesInstance()
    {
        var guard = new CardinalityGuard();
        Assert.NotNull(guard);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidThreshold_ThrowsArgumentOutOfRangeException(int threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CardinalityGuard(threshold));
    }

    [Fact]
    public void RecordAttributeValue_WithUniqueValues_TracksCardinality()
    {
        var guard = new CardinalityGuard(threshold: 10);

        guard.RecordAttributeValue("event.type", "LoadArrived");
        guard.RecordAttributeValue("event.type", "LoadDeparted");
        guard.RecordAttributeValue("event.type", "LoadGenerated");

        var stats = guard.GetStatistics();
        Assert.Equal(1, stats.TotalAttributes);
        Assert.Equal(3, stats.TotalUniqueValues);
        Assert.Equal(3, stats.AttributeCardinalities["event.type"]);
    }

    [Fact]
    public void RecordAttributeValue_WithDuplicateValues_DoesNotIncrease()
    {
        var guard = new CardinalityGuard(threshold: 10);

        guard.RecordAttributeValue("event.type", "LoadArrived");
        guard.RecordAttributeValue("event.type", "LoadArrived");
        guard.RecordAttributeValue("event.type", "LoadArrived");

        var stats = guard.GetStatistics();
        Assert.Equal(1, stats.TotalUniqueValues);
    }

    [Fact]
    public void RecordAttributeValue_WithNullValue_Ignored()
    {
        var guard = new CardinalityGuard(threshold: 10);

        guard.RecordAttributeValue("event.type", null!);

        var stats = guard.GetStatistics();
        Assert.Equal(0, stats.TotalUniqueValues);
    }

    [Fact]
    public void RecordAttributeValue_WithNullAttributeName_ThrowsArgumentException()
    {
        var guard = new CardinalityGuard(threshold: 10);

        Assert.Throws<ArgumentException>(() => guard.RecordAttributeValue(null!, "value"));
    }

    [Fact]
    public void RecordAttributeValue_WithEmptyAttributeName_ThrowsArgumentException()
    {
        var guard = new CardinalityGuard(threshold: 10);

        Assert.Throws<ArgumentException>(() => guard.RecordAttributeValue("", "value"));
    }

    [Fact]
    public void RecordAttributeValue_ExceedingThreshold_RaisesWarning()
    {
        var guard = new CardinalityGuard(threshold: 3);
        bool warningRaised = false;
        string? warningAttribute = null;
        int warningCardinality = 0;

        guard.CardinalityWarning += (sender, args) =>
        {
            warningRaised = true;
            warningAttribute = args.AttributeName;
            warningCardinality = args.CurrentCardinality;
        };

        guard.RecordAttributeValue("event.type", "Type1");
        guard.RecordAttributeValue("event.type", "Type2");
        guard.RecordAttributeValue("event.type", "Type3");
        Assert.False(warningRaised); // Not yet exceeded

        guard.RecordAttributeValue("event.type", "Type4");
        Assert.True(warningRaised);
        Assert.Equal("event.type", warningAttribute);
        Assert.Equal(4, warningCardinality);
    }

    [Fact]
    public void RecordAttributeValue_MultipleAttributes_TracksIndependently()
    {
        var guard = new CardinalityGuard(threshold: 10);

        guard.RecordAttributeValue("event.type", "LoadArrived");
        guard.RecordAttributeValue("event.type", "LoadDeparted");
        guard.RecordAttributeValue("server.id", "Server1");
        guard.RecordAttributeValue("server.id", "Server2");

        var stats = guard.GetStatistics();
        Assert.Equal(2, stats.TotalAttributes);
        Assert.Equal(4, stats.TotalUniqueValues);
        Assert.Equal(2, stats.AttributeCardinalities["event.type"]);
        Assert.Equal(2, stats.AttributeCardinalities["server.id"]);
    }

    [Fact]
    public void Reset_ClearsAllData()
    {
        var guard = new CardinalityGuard(threshold: 10);

        guard.RecordAttributeValue("event.type", "Type1");
        guard.RecordAttributeValue("event.type", "Type2");
        guard.RecordAttributeValue("server.id", "Server1");

        var statsBefore = guard.GetStatistics();
        Assert.Equal(2, statsBefore.TotalAttributes);
        Assert.Equal(3, statsBefore.TotalUniqueValues);

        guard.Reset();

        var statsAfter = guard.GetStatistics();
        Assert.Equal(0, statsAfter.TotalAttributes);
        Assert.Equal(0, statsAfter.TotalUniqueValues);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectStats()
    {
        var guard = new CardinalityGuard(threshold: 10);

        guard.RecordAttributeValue("event.type", "Type1");
        guard.RecordAttributeValue("event.type", "Type2");
        guard.RecordAttributeValue("event.type", "Type3");
        guard.RecordAttributeValue("server.id", "Server1");

        var stats = guard.GetStatistics();
        Assert.Equal(2, stats.TotalAttributes);
        Assert.Equal(4, stats.TotalUniqueValues);
        Assert.Equal(3, stats.MaxCardinality);
        Assert.Equal(3, stats.AttributeCardinalities["event.type"]);
        Assert.Equal(1, stats.AttributeCardinalities["server.id"]);
    }

    [Fact]
    public void UniqueValuesPerAttribute_ReturnsCorrectCounts()
    {
        var guard = new CardinalityGuard(threshold: 10);

        guard.RecordAttributeValue("event.type", "Type1");
        guard.RecordAttributeValue("event.type", "Type2");
        guard.RecordAttributeValue("server.id", "Server1");

        var uniqueValues = guard.UniqueValuesPerAttribute;
        Assert.Equal(2, uniqueValues["event.type"]);
        Assert.Equal(1, uniqueValues["server.id"]);
    }

    [Fact]
    public void TotalUniqueValues_ReturnsCorrectCount()
    {
        var guard = new CardinalityGuard(threshold: 10);

        guard.RecordAttributeValue("event.type", "Type1");
        guard.RecordAttributeValue("event.type", "Type2");
        guard.RecordAttributeValue("server.id", "Server1");

        Assert.Equal(3, guard.TotalUniqueValues);
    }

    [Fact]
    public void Dispose_AllowsMultipleCalls()
    {
        var guard = new CardinalityGuard(threshold: 10);
        guard.RecordAttributeValue("event.type", "Type1");

        guard.Dispose();
        guard.Dispose(); // Should not throw
    }

    [Fact]
    public void RecordAttributeValue_AfterDispose_ThrowsObjectDisposedException()
    {
        var guard = new CardinalityGuard(threshold: 10);
        guard.Dispose();

        Assert.Throws<ObjectDisposedException>(() => guard.RecordAttributeValue("event.type", "Type1"));
    }

    [Fact]
    public void GetStatistics_AfterDispose_ThrowsObjectDisposedException()
    {
        var guard = new CardinalityGuard(threshold: 10);
        guard.Dispose();

        Assert.Throws<ObjectDisposedException>(() => guard.GetStatistics());
    }

    [Fact]
    public void Reset_AfterDispose_ThrowsObjectDisposedException()
    {
        var guard = new CardinalityGuard(threshold: 10);
        guard.Dispose();

        Assert.Throws<ObjectDisposedException>(() => guard.Reset());
    }

    [Fact]
    public void ConcurrentAccess_TracksCardinalityCorrectly()
    {
        var guard = new CardinalityGuard(threshold: 1000);
        var tasks = new List<Task>();

        // Simulate concurrent access from multiple threads
        for (int i = 0; i < 10; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    guard.RecordAttributeValue("event.type", $"Type{threadId}_{j}");
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        var stats = guard.GetStatistics();
        Assert.Equal(1, stats.TotalAttributes);
        Assert.Equal(1000, stats.TotalUniqueValues); // 10 threads * 100 unique values each
    }
}
