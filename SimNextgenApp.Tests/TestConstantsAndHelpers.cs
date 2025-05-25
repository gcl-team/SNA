namespace SimNextgenApp.Tests;

public static class TestConstants
{
    /// <summary>
    /// Default tolerance for comparing double-precision floating-point numbers in tests.
    /// </summary>
    public const double Epsilon = 0.000001;
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