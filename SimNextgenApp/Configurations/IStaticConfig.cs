namespace SimNextgenApp.Configurations;

/// <summary>
/// A marker interface used to identify types that represent static configurations
/// for simulation components.
/// </summary>
/// <remarks>
/// This interface is intentionally empty. Its purpose is to allow for compile-time
/// identification and grouping of various configuration classes. Classes implementing
/// this interface define parameters that are typically set before a simulation run
/// and remain constant throughout its execution.
/// <para>
/// For example, a class configuring a 'Generator' or a 'Server' component would
/// implement <see cref="IStaticConfig"/>.
/// </para>
/// </remarks>
public interface IStaticConfig
{
    // This interface is intentionally left empty as it serves as a marker.
    // No common members are defined at this base level for all static configurations.
}