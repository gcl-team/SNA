namespace SimNextgenApp.Exceptions;

/// <summary>
/// The exception thrown when a simulation run fails, such as when model initialization throws,
/// an event fails during execution, or the simulation state becomes inconsistent.
/// </summary>
public class SimulationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationException"/> class with an error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SimulationException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationException"/> class with an error message
    /// and the exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that caused this exception.</param>
    public SimulationException(string message, Exception inner)
        : base(message, inner) { }
}