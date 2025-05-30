using SimNextgenApp.Core;

namespace SimNextgenApp.Tests.Core;

public class TestRunContext(IScheduler scheduler, double initialClockTime = 0.0, long initialEventCount = 0) : IRunContext
{
    public IScheduler Scheduler { get; set; } = scheduler;
    public double ClockTime { get; set; } = initialClockTime;
    public long ExecutedEventCount { get; set; } = initialEventCount;
}