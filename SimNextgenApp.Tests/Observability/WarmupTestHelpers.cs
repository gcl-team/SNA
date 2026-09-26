using System.Diagnostics.Metrics;
using SimNextgenApp.Core;

namespace SimNextgenApp.Tests.Observability;

/// <summary>
/// Sets the engine's warmup state for the current thread, as a running engine would,
/// and restores the previous value when disposed.
/// </summary>
internal sealed class WarmupPhaseScope : IDisposable
{
    private readonly bool _previous;

    public WarmupPhaseScope(bool isActive)
    {
        _previous = WarmupPhase.IsActive;
        WarmupPhase.IsActive = isActive;
    }

    public void Dispose() => WarmupPhase.IsActive = _previous;
}

/// <summary>
/// Records the <c>sna.simulation.warmup</c> label of every measurement taken by one <see cref="Meter"/>.
/// </summary>
/// <remarks>
/// Every observer's meter has the same name, and xUnit runs test classes in parallel. Listening to
/// this meter instance only, rather than to the meter name, keeps other tests' measurements out.
/// </remarks>
internal sealed class WarmupLabelRecorder : IDisposable
{
    private readonly MeterListener _listener;
    private readonly List<(string Instrument, bool? Warmup)> _labels = [];

    public WarmupLabelRecorder(Meter meter)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        _listener.SetMeasurementEventCallback<int>((instrument, _, tags, _) => Record(instrument, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => Record(instrument, tags));
        _listener.Start();
    }

    /// <summary>
    /// The warmup label of each measurement from the named instrument, in the order they were taken.
    /// Null means the measurement had no warmup label.
    /// </summary>
    public IReadOnlyList<bool?> LabelsFor(string instrumentName) =>
        _labels.Where(l => l.Instrument == instrumentName).Select(l => l.Warmup).ToList();

    /// <summary>
    /// Asks the meter's observable instruments, such as gauges, to report their current value.
    /// </summary>
    public void RecordObservableInstruments() => _listener.RecordObservableInstruments();

    public void Dispose() => _listener.Dispose();

    private void Record(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        bool? warmup = null;
        foreach (var tag in tags)
        {
            if (tag.Key == "sna.simulation.warmup") warmup = tag.Value as bool?;
        }
        _labels.Add((instrument.Name, warmup));
    }
}
