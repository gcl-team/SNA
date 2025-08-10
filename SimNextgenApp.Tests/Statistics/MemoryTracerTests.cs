using SimNextgenApp.Statistics;

namespace SimNextgenApp.Tests.Statistics;

public class MemoryTracerTests
{
    [Fact(DisplayName = "A new MemoryTracer should start with an empty list of records.")]
    public void Constructor_WhenCalled_InitializesEmptyRecordList()
    {
        // Arrange & Act
        var tracer = new MemoryTracer();

        // Assert
        Assert.NotNull(tracer.Records);
        Assert.Empty(tracer.Records);
    }


    [Fact(DisplayName = "Trace should add a single valid record to the list.")]
    public void Trace_WithValidRecord_AddsRecordToList()
    {
        // Arrange
        var tracer = new MemoryTracer();
        var record = new TraceRecord(TracePoint.EventExecuting, 1.0, 1, "TestEvent");

        // Act
        tracer.Trace(record);

        // Assert
        Assert.Single(tracer.Records);
        Assert.Same(record, tracer.Records[0]);
    }

    [Fact(DisplayName = "Trace should append multiple records to the list in the order they are traced.")]
    public void Trace_WhenCalledMultipleTimes_AddsAllRecordsInOrder()
    {
        // Arrange
        var tracer = new MemoryTracer();
        var record1 = new TraceRecord(TracePoint.EventExecuting, 1.0, 1, "Event1");
        var record2 = new TraceRecord(TracePoint.EventCompleted, 1.5, 1, "Event1");

        // Act
        tracer.Trace(record1);
        tracer.Trace(record2);

        // Assert
        Assert.Equal(2, tracer.Records.Count);
        Assert.Same(record1, tracer.Records[0]);
        Assert.Same(record2, tracer.Records[1]);
    }

    [Fact(DisplayName = "Trace should throw ArgumentNullException when given a null record.")]
    public void Trace_WithNullRecord_ThrowsArgumentNullException()
    {
        // Arrange
        var tracer = new MemoryTracer();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(
            () => tracer.Trace(null!));
        Assert.Equal("record", ex.ParamName);
    }
}
