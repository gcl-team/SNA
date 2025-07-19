namespace SimNextgenApp.Statistics;

/// <summary>
/// Tracks time-based metrics including counts, increments, and decrements over simulation time.
/// Provides statistical calculations like time-weighted averages, rates, percentiles, and histograms.
/// </summary>
public class TimeBasedMetric
{
    private List<(double Time, double CountValue)> _history;

    /// <summary>
    /// The starting time of the observation period.
    /// </summary>
    public double InitialTime { get; private set; }

    /// <summary>
    /// The current simulation time at the latest observation.
    /// </summary>
    public double CurrentTime { get; private set; }

    /// <summary>
    /// The count value recorded at the current simulation time.
    /// </summary>
    public double CurrentCount { get; private set; }

    /// <summary>
    /// Total sum of all positive changes to the count observed.
    /// </summary>
    public double TotalIncrementObserved { get; private set; }

    /// <summary>
    /// Total sum of all positive magnitudes of negative changes to the count observed.
    /// </summary>
    public double TotalDecrementObserved { get; private set; }

    /// <summary>
    /// Total simulation time elapsed during which metrics were actively recorded
    /// (sum of durations between observations).
    /// </summary>
    public double TotalActiveDuration { get; private set; }

    /// <summary>
    /// The average increment per unit of active simulation time.
    /// </summary>
    public double IncrementRate => TotalActiveDuration == 0 ? 0 : TotalIncrementObserved / TotalActiveDuration;

    /// <summary>
    /// The average decrement per unit of active simulation time.
    /// </summary>
    public double DecrementRate => TotalActiveDuration == 0 ? 0 : TotalDecrementObserved / TotalActiveDuration;

    /// <summary>
    /// Ratio of the total active duration (where metrics were recorded) to the
    /// total simulation time span from InitialTime to CurrentTime.
    /// A value of 1 indicates observations covered the entire span.
    /// </summary>
    public double ObservationCoverageRatio => (CurrentTime == InitialTime)
      ? 0 : TotalActiveDuration / (CurrentTime - InitialTime);

    /// <summary>
    /// The cumulative sum of (count × elapsed time while at that count),
    /// representing the total count-time product. Essential for time-weighted averages.
    /// </summary>
    public double CumulativeCountTimeProduct { get; private set; }

    /// <summary>
    /// The time-weighted average count over the total active observation duration.
    /// If no active duration, returns the current count.
    /// </summary>
    public double AverageCount => TotalActiveDuration == 0 ? CurrentCount : CumulativeCountTimeProduct / TotalActiveDuration;

    /// <summary>
    /// Estimates the average duration an item stays in the system,
    /// assuming a stationary process (decrement rate approximates arrival/throughput rate).
    /// This is an application of Little's Law (L = λW  => W = L/λ).
    /// Returns 0 if decrement rate is zero or results in NaN/Infinity.
    /// </summary>
    public double AverageSojournTime
    {
        get
        {
            if (DecrementRate == 0) return 0;
            double duration = AverageCount / DecrementRate;
            return (double.IsNaN(duration) || double.IsInfinity(duration)) ? 0 : duration;
        }
    }

    /// <summary>
    /// Indicates whether detailed time-series history is recorded for each observed count value.
    /// </summary>
    public bool IsHistoryEnabled { get; private set; }

    /// <summary>
    /// Tracks the total observed time spent at each (rounded) integer count level.
    /// Key: Rounded count value. Value: Total duration spent at that count.
    /// </summary>
    public IReadOnlyDictionary<int, double> TimePerCount => TimeForCountInternal;
    private SortedDictionary<int, double> TimeForCountInternal { get; set; }


    /// <summary>
    /// Returns the historical record of count observations over time.
    /// Each entry is a (Time, CountValue) pair.
    /// Returned only if history tracking is enabled; otherwise, an empty list.
    /// The list is sorted by time.
    /// </summary>
    public IReadOnlyList<(double Time, double CountValue)> History =>
        IsHistoryEnabled && _history != null
            ? _history.OrderBy(p => p.Time).ToList().AsReadOnly()
            : Array.Empty<(double Time, double CountValue)>();

    public TimeBasedMetric(double initialTime = 0.0, bool enableHistory = false)
    {
        // Enforce initialTime is not negative, though DES time usually starts at 0
        if (initialTime < 0)
            throw new ArgumentOutOfRangeException(nameof(initialTime), "Initial time cannot be negative.");

        Init(initialTime, enableHistory);
        TimeForCountInternal = [];
        _history = [];
    }

    /// <summary>
    /// Observes the current count at a given simulation clock time and updates the metrics accordingly.
    /// Tracks increments and decrements in count relative to the last observation,
    /// updates cumulative totals, and optionally records history if enabled.
    /// </summary>
    /// <param name="count">The current observed count value.</param>
    /// <param name="clockTime">The simulation clock time at which the count is observed. Must not be less than CurrentTime.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if clockTime is less than CurrentTime.</exception>
    public void ObserveCount(double count, double clockTime)
    {
        if (clockTime < CurrentTime)
        {
            throw new ArgumentOutOfRangeException(nameof(clockTime), $"New clock time ({clockTime}) cannot be less than current time ({CurrentTime}). Events must be processed in chronological order.");
        }

        // Calculate change from previous CurrentCount *before* updating CurrentCount
        if (count > CurrentCount)
        {
            TotalIncrementObserved += count - CurrentCount;
        }
        else if (count < CurrentCount) // Use else if to avoid issues if count == CurrentCount
        {
            TotalDecrementObserved += CurrentCount - count;
        }

        if (clockTime > CurrentTime)
        {
            var duration = clockTime - CurrentTime;
            TotalActiveDuration += duration;
            CumulativeCountTimeProduct += duration * CurrentCount; // Use CurrentCount *before* it's updated for this interval

            // Update time spent at the (previous) CurrentCount
            int countKey = (int)Math.Round(CurrentCount);
            TimeForCountInternal.TryGetValue(countKey, out double currentDuration);
            TimeForCountInternal[countKey] = currentDuration + duration;
        }

        // Update current state *after* processing the duration at the *previous* state
        CurrentTime = clockTime;
        CurrentCount = count;

        if (IsHistoryEnabled)
        {
            _history?.Add((clockTime, count));
        }
    }

    /// <summary>
    /// Observes a change (increment or decrement) to the current count at a specified simulation clock time.
    /// Internally updates the count by adding the change to the current count and delegates to <see cref="ObserveCount"/>.
    /// </summary>
    /// <param name="change">The change amount to apply to the current count (positive for increment, negative for decrement).</param>
    /// <param name="clockTime">The simulation clock time at which the change is observed. Must not be less than CurrentTime.</param>
    public void ObserveChange(double change, double clockTime)
    {
        ObserveCount(CurrentCount + change, clockTime);
    }

    /// <summary>
    /// Resets the metric state and starts measuring from the specified clock time.
    /// This is typically used after a warm-up period in a simulation,
    /// to discard initial transient data and collect steady-state statistics.
    /// </summary>
    /// <param name="clockTime">The simulation time from which to start fresh measurement. Must not be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if clockTime is negative.</exception>
    public void WarmedUp(double clockTime, double currentCountAtWarmup)
    {
        if (clockTime < 0)
            throw new ArgumentOutOfRangeException(nameof(clockTime), "Clock time for warm-up cannot be negative.");

        // Preserve history setting, re-initialize other fields
        Init(clockTime, IsHistoryEnabled);

        CurrentCount = currentCountAtWarmup; // Set the new baseline count

        if (IsHistoryEnabled)
        {
            _history?.Add((clockTime, CurrentCount));
        }
    }

    /// <summary>
    /// Calculates the count value at the specified percentile of total active observation time.
    /// For example, a ratio of 90 returns the count value X such that the system
    /// spent 90% of its active time at or below count X.
    /// </summary>
    /// <param name="percentileRatio">Percentile ratio (0 to 100). For example, 95 for 95th percentile.</param>
    /// <returns>
    /// The count value at the given percentile of active observation time.
    /// Returns the smallest observed count if percentile is 0.
    /// Returns the largest observed count if no data or if percentile is 100.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="percentileRatio"/> is not between 0 and 100.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no observation data is available (TotalActiveDuration is zero).</exception>
    public int GetCountPercentileByTime(double percentileRatio)
    {
        if (percentileRatio < 0 || percentileRatio > 100)
            throw new ArgumentOutOfRangeException(nameof(percentileRatio), "Percentile ratio must be between 0 and 100.");

        if (TimeForCountInternal == null || TimeForCountInternal.Count == 0 || TotalActiveDuration == 0)
        {
            if (TimeForCountInternal != null && TimeForCountInternal.Any())
            {
                return TimeForCountInternal.Keys.DefaultIfEmpty(0).First(); // if TotalActiveDuration is 0 but there's a single point
            }

            return 0; // Default if truly empty
        }

        // Use TotalActiveDuration which is already calculated
        double thresholdTime = TotalActiveDuration * percentileRatio / 100.0;
        double cumulativeTime = 0;

        // TimeForCountInternal is a SortedDictionary, so keys are already sorted.
        foreach (var kvp in TimeForCountInternal)
        {
            cumulativeTime += kvp.Value;
            if (cumulativeTime >= thresholdTime)
                return kvp.Key;
        }

        // Should only be reached if percentileRatio is 100 
        // (or due to tiny FP issues making cumulativeTime slightly less than TotalActiveDuration)
        // In this case, return the highest count observed.
        return TimeForCountInternal.Keys.LastOrDefault();
    }


    /// <summary>
    /// Generates a histogram summarizing the total time, probability, and cumulative probability
    /// spent at each count value interval.
    /// </summary>
    /// <param name="countIntervalWidth">Width of each count interval/bin. Must be positive.</param>
    /// <returns>
    /// A list of histogram bins, each containing the lower bound of the interval,
    /// total observed time in that interval, probability, and cumulative probability.
    /// Returns an empty list if no observation data is available.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if countIntervalWidth is not positive.</exception>
    public List<HistogramBin> GenerateHistogram(double countIntervalWidth)
    {
        if (countIntervalWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(countIntervalWidth), "Count interval width must be positive.");

        var histogram = new List<HistogramBin>();

        if (TimeForCountInternal == null || TimeForCountInternal.Count == 0 || TotalActiveDuration == 0)
            return histogram;

        // Determine min and max count values to cover entire range
        // Keys are sorted due to SortedDictionary
        double minObservedCount = TimeForCountInternal.Keys.First();
        double maxObservedCount = TimeForCountInternal.Keys.Last();

        // Normalise minCount to nearest lower multiple of countIntervalWidth
        double currentBinLowerBound = Math.Floor(minObservedCount / countIntervalWidth) * countIntervalWidth;

        int keyIndex = 0;
        var sortedKeys = TimeForCountInternal.Keys.ToList();

        while (currentBinLowerBound <= maxObservedCount)
        {
            double currentBinUpperBound = currentBinLowerBound + countIntervalWidth;
            double accumulatedTimeInBin = 0;

            // Accumulate total time for counts within [currentBinLowerBound, currentBinUpperBound)
            while (keyIndex < sortedKeys.Count && sortedKeys[keyIndex] < currentBinUpperBound)
            {
                // Ensure the key actually exists, though it should if sortedKeys[keyIndex] came from TimeForCountInternal
                if (TimeForCountInternal.TryGetValue(sortedKeys[keyIndex], out double timeAtThisCount))
                {
                    accumulatedTimeInBin += timeAtThisCount;
                }
                keyIndex++;
            }

            histogram.Add(new HistogramBin(
                countLowerBound: currentBinLowerBound,
                totalTime: accumulatedTimeInBin,
                probability: 0,
                cumulativeProbability: 0
            ));

            currentBinLowerBound = currentBinUpperBound;
        }

        // Calculate probabilities using the authoritative TotalActiveDuration
        if (TotalActiveDuration > 0)
        {
            double cumulativeProbability = 0.0;
            for (int i = 0; i < histogram.Count; i++)
            {
                var bin = histogram[i];
                double probability = bin.TotalTime / TotalActiveDuration;
                cumulativeProbability += probability;

                // Create new instance if HistogramBin is immutable, or modify if mutable
                // Assuming HistogramBin is immutable with init setters and constructor:
                histogram[i] = new HistogramBin(
                    bin.CountLowerBound,
                    bin.TotalTime,
                    probability,
                    cumulativeProbability
                );
            }
        }
        return histogram;
    }

    private void Init(double initialTime, bool enabledHistory)
    {
        InitialTime = initialTime;
        CurrentTime = initialTime;
        CurrentCount = 0;

        TotalIncrementObserved = 0;
        TotalDecrementObserved = 0;
        TotalActiveDuration = 0;

        CumulativeCountTimeProduct = 0;
        TimeForCountInternal = [];

        IsHistoryEnabled = enabledHistory;
        _history = [];
    }
}

/// <summary>
/// Represents a single bin (interval) in a histogram,
/// containing statistics about the time spent within a specific count range.
/// </summary>
public class HistogramBin(double countLowerBound, double totalTime, double probability, double cumulativeProbability)
{
    /// <summary>
    /// The lower bound of the count interval represented by this bin.
    /// </summary>
    public double CountLowerBound { get; init; } = countLowerBound;

    /// <summary>
    /// The total time observed (e.g. in hours or other simulation time units)
    /// for counts falling within this bin's interval.
    /// </summary>
    public double TotalTime { get; init; } = totalTime;

    /// <summary>
    /// The probability of observations falling within this bin,
    /// calculated as (TotalTime in this bin) / (TotalActiveDuration of the metric).
    /// </summary>
    public double Probability { get; init; } = probability;

    /// <summary>
    /// The cumulative probability of observations up to and including this bin.
    /// </summary>
    public double CumulativeProbability { get; init; } = cumulativeProbability;
}