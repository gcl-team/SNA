using SimNextgenApp.Statistics;

namespace SimNextgenApp.Tests.Statistics;

public class MemoryTracerTests
{
    [Fact]
    public void Trace_AddsRecord_ToList()
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
}
