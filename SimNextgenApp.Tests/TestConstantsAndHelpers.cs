using SimNextgenApp.Core;
using SimNextgenApp.Events;

namespace SimNextgenApp.Tests;

public static class TestConstants
{
    /// <summary>
    /// Default tolerance for comparing double-precision floating-point numbers in tests.
    /// </summary>
    public const double Epsilon = 0.000001;
}

/// <summary>
/// A simple, anemic data class used as a placeholder for TLoad in unit tests,
/// particularly for testing generic components like Generator<TLoad> or Server<TLoad>.
/// </summary>
public class DummyLoad
{
    private static int _instanceCounter = 0;

    /// <summary>
    /// A unique ID for differentiating dummy load instances if needed during testing.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// An optional value that can be set by a test LoadFactory to verify creation.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// An optional tag for debugging or specific test scenarios.
    /// </summary>
    public string? Tag { get; set; }

    public DummyLoad(string? tag = null)
    {
        Id = System.Threading.Interlocked.Increment(ref _instanceCounter);
        Tag = tag;
    }

    public override string ToString()
    {
        return $"DummyLoad{(string.IsNullOrWhiteSpace(Tag) ? "" : $"({Tag})")}#{Id}";
    }
}

public static class AssertHelpers
{
    /// <summary>
    /// Asserts that two double values are equal within the predefined Epsilon tolerance.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    public static void AreEqual(double expected, double actual)
    {
        Assert.Equal(expected, actual, TestConstants.Epsilon);
    }
}

public class DummyLoadType
{
    
}

public class TestEvent : AbstractEvent
{
    public bool Executed { get; private set; } = false;

    public override void Execute(IRunContext engine) => Executed = true;
}

public class NamedEvent(string label, List<string> executionList) : AbstractEvent
{
    private readonly string _label = label;
    private readonly List<string> _executionList = executionList;

    public override void Execute(IRunContext engine) => _executionList.Add(_label);
}