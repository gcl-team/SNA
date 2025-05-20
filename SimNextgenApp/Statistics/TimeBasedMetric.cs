namespace SimNextgenApp.Statistics;

/// <summary>
/// Tracks time-based metrics including hourly counts, increments, and decrements over time.
/// Provides statistical calculations based on the observed data.
/// </summary>
public class TimeBasedMetric
{
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
    /// Total number of increment observed
    /// </summary>
    public double TotalIncrementCount { get; private set; }
    
    /// <summary>
    /// Total number of decrement observed
    /// </summary>
    public double TotalDecrementCount { get; private set; }
    
    /// <summary>
    /// Total simulation time elapsed since the initial time.
    /// </summary>
    public double TotalTime { get; private set; }

    /// <summary>
    /// The average increment per unit time.
    /// </summary>
    public double IncrementRate => TotalTime == 0 ? 0 : TotalIncrementCount / TotalTime;

    /// <summary>
    /// The average decrement per unit time .
    /// </summary>
    public double DecrementRate => TotalTime == 0 ? 0 : TotalDecrementCount / TotalTime;

    public double WorkingTimeRatio => (CurrentTime == InitialTime)
      ? 0 : TotalTime / (CurrentTime - InitialTime);
    
    /// <summary>
    /// The cumulative sum of (count × elapsed time), representing the total count accumulated over time.
    ///
    /// In DES, it's common to track things like average queue length, average resource utilisation, or 
    /// average number of customers over time — all of which rely on summing count × duration over time.
    /// </summary>
    public double CumulativeCount { get; private set; }
    
    /// <summary>
    /// The average count over the total observed simulation time.
    /// </summary>
    public double AverageCount => TotalTime == 0 ? CurrentCount : CumulativeCount / TotalTime;

    /// <summary>
    /// Average duration that a load stays in the activity, assuming a stationary process, i.e. decrement 
    /// rate == increment rate.
    /// Returns 0 if no decrement observed (initial state).
    /// </summary>
    public double AverageDurationInHours
    {
        get
        {
            double duration = AverageCount / DecrementRate;
            return (double.IsNaN(duration) || double.IsInfinity(duration)) ? 0 : duration;
        }
    }

    /// <summary>
    /// Indicates whether detailed time-series history is recorded for each observed count value.
    /// </summary>
    public bool IsHistoryEnabled { get; private set; }
    
    /// <summary>
    /// Tracks the total observed time spent at each count level.
    /// </summary>
    public SortedDictionary<int, double> TimeForCount { get; private set; }

    /// <summary>
    /// Returns the historical record of count observations over time.
    /// Each entry is a (time, count) pair.
    /// Returned only if history tracking is enabled; otherwise, an empty list.
    /// </summary>
    public List<(double Time, double Count)> History =>
      IsHistoryEnabled
          ? _history.OrderBy(p => p.Time).ToList()
          : [];

    public TimeBasedMetric(double initialTime = 0.0, bool isHistoryEnabled = false)
    {
        Init(initialTime, isHistoryEnabled);
    }

    /// <summary>
    /// Observes the current count at a given simulation clock time and updates the metrics accordingly.
    /// Tracks increments and decrements in count relative to the last observation,
    /// updates cumulative totals, and optionally records history if enabled.
    /// </summary>
    /// <param name="count">The current observed count value.</param>
    /// <param name="clockTime">The simulation clock time at which the count is observed.</param>
    public void ObserveCount(double count, double clockTime)
    {
        // Keeps track of how much the count moved up/down cumulatively.
        if (count > CurrentCount)
        {
            TotalIncrementCount += count - CurrentCount;
        }
        else
        {
            TotalDecrementCount += CurrentCount - count;
        }

        if (clockTime > CurrentTime)
        {
            var duration = clockTime - CurrentTime;
            TotalTime += duration;
            CumulativeCount += duration * CurrentCount;
            CurrentTime = clockTime;

            int countKey = (int)Math.Round(CurrentCount);
            if (!TimeForCount.ContainsKey(countKey))
            {
                TimeForCount[countKey] = 0;
            }
            TimeForCount[countKey] += duration;
        }
        
        CurrentCount = count;

        if (IsHistoryEnabled)
        {
            _history.Add((clockTime, count));
        }
    }

    /// <summary>
    /// Observes a change (increment or decrement) to the current count at a specified simulation clock time.
    /// Internally updates the count by adding the change to the current count and delegates to <see cref="ObserveCount"/>.
    /// </summary>
    /// <param name="change">The change amount to apply to the current count (positive or negative).</param>
    /// <param name="clockTime">The simulation clock time at which the change is observed.</param>
    public void ObserveChange(double change, double clockTime)
    {
        ObserveCount(CurrentCount + change, clockTime);
    }

    /// <summary>
    /// Resets the metric state and starts measuring from the specified clock time.
    /// This is typically used to indicate that the metric has 'warmed up' or
    /// that the system has reached a stable state from which measurements should begin.
    /// </summary>
    /// <param name="clockTime">The simulation time from which to start fresh measurement.</param>
    public void WarmedUp(double clockTime)
    {
        Init(clockTime, IsHistoryEnabled);
    }

    /// <summary>
    /// Calculates the count value at the specified percentile of observation time.
    /// That is, returns the count value below which the system has spent the given percentage of total observation time.
    /// </summary>
    /// <param name="ratio">Percentile ratio (0 to 100).</param>
    /// <returns>
    /// The count value at the given percentile of observation time.
    /// Returns the maximum count if the percentile is 100.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="ratio"/> is not between 0 and 100.</exception>
    public int GetCountPercentileByTime(double ratio)
    {
        if (ratio < 0 || ratio > 100)
            throw new ArgumentOutOfRangeException(nameof(ratio), "Percentile ratio must be between 0 and 100.");
    
        if (TimeForCount == null || TimeForCount.Count == 0)
            throw new InvalidOperationException("TimeForCount is empty or not initialized.");
    
        double totalTime = TimeForCount.Values.Sum();
        if (totalTime == 0) 
            return TimeForCount.Keys.First();
    
        double threshold = totalTime * ratio / 100.0;
    
        foreach (var kvp in TimeForCount)
        {
            threshold -= kvp.Value;
            if (threshold <= 0)
                return kvp.Key;
        }
    
        // If ratio == 100, return the max key (highest count observed)
        return TimeForCount.Keys.Last();
    }

    
    /// <summary>
    /// Generates a histogram summarizing the total time, probability, and cumulative probability
    /// spent at each count value interval.
    /// </summary>
    /// <param name="countInterval">Width of each count interval/bin.</param>
    /// <returns>
    /// A list of histogram bins, each containing the lower bound of the interval,
    /// total observed time in that interval, probability, and cumulative probability.
    /// </returns>
    public List<HistogramBin> GenerateHistogram(double countInterval)
    {
        if (countInterval <= 0)
            throw new ArgumentException("countInterval must be positive.", nameof(countInterval));
    
        var histogram = new List<HistogramBin>();
    
        if (TimeForCount == null || TimeForCount.Count == 0)
            return histogram;
    
        // Determine min and max count values to cover entire range
        double minCount = TimeForCount.Keys.Min();
        double maxCount = TimeForCount.Keys.Max();
    
        // Normalise minCount to nearest lower multiple of countInterval
        double currentLowerBound = Math.Floor(minCount / countInterval) * countInterval;
    
        int index = 0;
        var keys = TimeForCount.Keys.ToList();
    
        while (currentLowerBound <= maxCount)
        {
            double upperBound = currentLowerBound + countInterval;
            double accumulatedTimeInBin = 0;
    
            // Accumulate total hours for counts within [currentLowerBound, upperBound)
            while (index < keys.Count && keys[index] < upperBound)
            {
                accumulatedTimeInBin += TimeForCount[keys[index]];
                index++;
            }
    
            histogram.Add(new HistogramBin
            {
                LowerBound = currentLowerBound,
                TotalHours = accumulatedTimeInBin
            });
    
            currentLowerBound = upperBound;
        }
    
        // Calculate sum of all hours for probability computation
        double totalTime = histogram.Sum(bin => bin.TotalTime);
    
        if (totalTime > 0)
        {
            double cumulativeProbability = 0.0;
            foreach (var bin in histogram)
            {
                bin.Probability = bin.TotalTime / totalTime;
                cumulativeProbability += bin.Probability;
                bin.CumulativeProbability = cumulativeProbability;
            }
        }
    
        return histogram;
    }
  
    private List<(double Time, double Count)> _history;
    
    private void Init(double initialTime, bool isHistoryEnabled)
    {
        InitialTime = initialTime;
        CurrentTime = initialTime;
        CurrentCount = 0;
      
        TotalIncrementCount = 0;
        TotalDecrementCount = 0;
        TotalTime = 0;
      
        CumulativeCount = 0;
        TimeForCount = new SortedDictionary<int, double>();
      
        IsHistoryEnabled = isHistoryEnabled;
        
        if (isHistoryEnabled)
        {
            _history = [];
        }
    }
    
}

/// <summary>
/// Represents a single bin (interval) in a histogram, 
/// containing statistics about the time spent within a specific count range.
/// </summary>
public class HistogramBin
{
    /// <summary>
    /// The lower bound of the count interval represented by this bin.
    /// </summary>
    public double CountLowerBound { get; set; }
    
    /// <summary>
    /// The total time observed within this count interval.
    /// </summary>
    public double TotalTime { get; set; }
    
    /// <summary>
    /// The probability of observations falling within this bin relative to the total observed time.
    /// </summary>
    public double Probability { get; set; }
    
    /// <summary>
    /// The cumulative probability of observations up to and including this bin.
    /// </summary>
    public double CumulativeProbability { get; set; }
}
