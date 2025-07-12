namespace SimNextgenApp.Modeling.Generator;

internal interface IGenerator<TLoad> : IWarmupAware
{
    /// <summary>
    /// Gets the unique identifier assigned to this simulation model instance.
    /// Typically assigned during instantiation.
    /// </summary>
    long Id { get; }

    /// <summary>
    /// Gets a descriptive, human-readable name for this simulation model instance.
    /// Useful for logging and results reporting. Typically set during instantiation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the simulation time when the generator was last started or when the warm-up period ended.
    /// This value is set when the generator becomes active or after WarmedUp.
    /// Will be <c>null</c> if the generator has not yet started or warmed up.
    /// The unit of time is consistent with the simulation engine's clock.
    /// </summary>
    double? StartTime { get; }

    /// <summary>
    /// Gets a value indicating whether the generator is currently active and producing loads.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the total number of loads generated since the last start or warm-up.
    /// </summary>
    int LoadsGeneratedCount { get; }

    /// <summary>
    /// Occurs when a new load is generated.
    /// </summary>
    event Action<TLoad, double>? LoadGenerated;
}
