namespace ParagonStats.Core.Stats;

/// <summary>
/// One statistic's accumulator: a lifetime total plus a window mark. Rates
/// derive from what accumulated since the mark; resetting moves only the mark
/// (#126), so lifetime totals survive every reset.
/// </summary>
internal sealed class Counter
{
    private decimal _markValue;
    private TimeSpan _markElapsed;

    public decimal Total { get; private set; }

    public void Add(decimal amount) => Total += amount;

    public void Mark(TimeSpan elapsed)
    {
        _markValue = Total;
        _markElapsed = elapsed;
    }

    public MetricSnapshot Snapshot(TimeSpan elapsed) =>
        MetricSnapshot.Compute(Total - _markValue, elapsed - _markElapsed);
}
