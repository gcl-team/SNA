using SimNextgenApp.Core;

namespace SimNextgenApp.Tests.Core;

public class TestRunContext : IRunContext
{
    public double ClockTime { get; set; }
    public long ExecutedEventCount { get; set; }
}