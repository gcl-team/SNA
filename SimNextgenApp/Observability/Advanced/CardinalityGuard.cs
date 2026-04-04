using System.Collections.Concurrent;

namespace SimNextgenApp.Observability.Advanced;

/// <summary>
/// Monitors attribute cardinality to prevent cost explosions in observability backends.
/// Tracks unique values per attribute and raises warnings when thresholds are exceeded.
/// </summary>
public sealed class CardinalityGuard : IDisposable
{
    private readonly int _threshold;
    private readonly ConcurrentDictionary<string, HashSet<string>> _attributeValues;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Event raised when an attribute's cardinality exceeds the configured threshold.
    /// </summary>
    public event EventHandler<CardinalityWarningEventArgs>? CardinalityWarning;

    /// <summary>
    /// Gets the number of unique values tracked for each attribute.
    /// </summary>
    public IReadOnlyDictionary<string, int> UniqueValuesPerAttribute
    {
        get
        {
            lock (_lock)
            {
                return _attributeValues.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Count);
            }
        }
    }

    /// <summary>
    /// Gets the total number of unique values across all attributes.
    /// </summary>
    public int TotalUniqueValues
    {
        get
        {
            lock (_lock)
            {
                return _attributeValues.Values.Sum(set => set.Count);
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CardinalityGuard"/> class.
    /// </summary>
    /// <param name="threshold">The maximum number of unique values per attribute before warning (default: 1000).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when threshold is less than or equal to zero.</exception>
    public CardinalityGuard(int threshold = 1000)
    {
        if (threshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be greater than zero.");

        _threshold = threshold;
        _attributeValues = new ConcurrentDictionary<string, HashSet<string>>();
    }

    /// <summary>
    /// Records an attribute value and checks cardinality.
    /// </summary>
    /// <param name="attributeName">The name of the attribute.</param>
    /// <param name="value">The value of the attribute.</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
    public void RecordAttributeValue(string attributeName, string value)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
            throw new ArgumentException("Attribute name cannot be null or whitespace.", nameof(attributeName));

        if (value == null)
            throw new ArgumentNullException(nameof(value), "Attribute value cannot be null.");

        bool wasAdded = false;
        int currentCount = 0;

        lock (_lock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CardinalityGuard));

            var values = _attributeValues.GetOrAdd(attributeName, _ => new HashSet<string>());

            if (!values.Contains(value))
            {
                values.Add(value);
                wasAdded = true;
                currentCount = values.Count;
            }
        }

        // Check threshold outside of lock to avoid blocking
        if (wasAdded && currentCount > _threshold)
        {
            OnCardinalityWarning(new CardinalityWarningEventArgs(
                attributeName,
                currentCount,
                _threshold));
        }
    }

    /// <summary>
    /// Resets all tracked cardinality data.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CardinalityGuard));

            _attributeValues.Clear();
        }
    }

    /// <summary>
    /// Gets cardinality statistics for all tracked attributes.
    /// </summary>
    /// <returns>A record containing cardinality statistics.</returns>
    public CardinalityStatistics GetStatistics()
    {
        lock (_lock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CardinalityGuard));

            var attributeStats = _attributeValues.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Count);

            var totalValues = attributeStats.Values.Sum();
            var maxCardinality = attributeStats.Values.DefaultIfEmpty(0).Max();
            var attributeCount = attributeStats.Count;

            return new CardinalityStatistics(
                TotalAttributes: attributeCount,
                TotalUniqueValues: totalValues,
                MaxCardinality: maxCardinality,
                AttributeCardinalities: attributeStats);
        }
    }

    private void OnCardinalityWarning(CardinalityWarningEventArgs e)
    {
        CardinalityWarning?.Invoke(this, e);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _attributeValues.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Event arguments for cardinality warning events.
/// </summary>
public sealed class CardinalityWarningEventArgs : EventArgs
{
    /// <summary>
    /// Gets the name of the attribute that exceeded the threshold.
    /// </summary>
    public string AttributeName { get; }

    /// <summary>
    /// Gets the current number of unique values for the attribute.
    /// </summary>
    public int CurrentCardinality { get; }

    /// <summary>
    /// Gets the configured threshold that was exceeded.
    /// </summary>
    public int Threshold { get; }

    public CardinalityWarningEventArgs(string attributeName, int currentCardinality, int threshold)
    {
        AttributeName = attributeName;
        CurrentCardinality = currentCardinality;
        Threshold = threshold;
    }
}

/// <summary>
/// Statistics about tracked attribute cardinality.
/// </summary>
/// <param name="TotalAttributes">Total number of tracked attributes.</param>
/// <param name="TotalUniqueValues">Total number of unique values across all attributes.</param>
/// <param name="MaxCardinality">Maximum cardinality among all attributes.</param>
/// <param name="AttributeCardinalities">Dictionary mapping attribute names to their cardinality counts.</param>
public record CardinalityStatistics(
    int TotalAttributes,
    int TotalUniqueValues,
    int MaxCardinality,
    IReadOnlyDictionary<string, int> AttributeCardinalities);
