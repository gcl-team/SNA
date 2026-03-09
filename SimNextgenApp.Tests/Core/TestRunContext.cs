using SimNextgenApp.Core;

namespace SimNextgenApp.Tests.Core;

public class TestRunContext(IScheduler scheduler, long initialClockTime = 0L, long initialEventCount = 0L) : IRunContext
{
    public IScheduler Scheduler { get; set; } = scheduler;
    public long ClockTime { get; set; } = initialClockTime;
    public long ExecutedEventCount { get; set; } = initialEventCount;
}