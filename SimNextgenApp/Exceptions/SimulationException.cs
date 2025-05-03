namespace SimNextgenApp.Exceptions;

public class SimulationException : Exception
{
    public SimulationException(string message)
        : base(message) { }

    public SimulationException(string message, Exception inner)
        : base(message, inner) { }
}