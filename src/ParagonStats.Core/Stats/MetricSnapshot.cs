namespace ParagonStats.Core.Stats;

/// <summary>
/// The uniform shape every tracked statistic surfaces through (#124): a value
/// and its derived rates. Produced only by <see cref="Compute"/> so batch and
/// live modes share one rate calculation and differ only in the window they
/// supply.
/// </summary>
public readonly record struct MetricSnapshot(decimal Value, decimal PerMinute, decimal PerHour)
{
    /// <summary>A zero or negative window yields zero rates, never NaN or infinity.</summary>
    public static MetricSnapshot Compute(decimal value, TimeSpan window)
    {
        if (window <= TimeSpan.Zero)
        {
            return new MetricSnapshot(value, 0m, 0m);
        }

        decimal minutes = (decimal)window.TotalMinutes;
        decimal perMinute = value / minutes;
        return new MetricSnapshot(value, perMinute, perMinute * 60m);
    }
}
