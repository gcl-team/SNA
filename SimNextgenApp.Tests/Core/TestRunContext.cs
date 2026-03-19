using SimNextgenApp.Core;
using SimNextgenApp.Core.Utilities;

namespace SimNextgenApp.Tests.Core;

public class TestRunContext(IScheduler scheduler, long initialClockTime = 0L, long initialEventCount = 0L, SimulationTimeUnit timeUnit = SimulationTimeUnit.Milliseconds) : IRunContext
{
    public IScheduler Scheduler { get; set; } = scheduler;
    public long ClockTime { get; set; } = initialClockTime;
    public long ExecutedEventCount { get; set; } = initialEventCount;
    public SimulationTimeUnit TimeUnit { get; set; } = timeUnit;
}