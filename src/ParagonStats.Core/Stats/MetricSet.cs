namespace ParagonStats.Core.Stats;

/// <summary>
/// The tracked statistics as one surface: every stat accumulates into a
/// counter windowed over the shared pausable timer, and every stat reads back
/// as a <see cref="MetricSnapshot"/>. Reset one stat or all (#126).
/// </summary>
public sealed class MetricSet
{
    private readonly Dictionary<StatId, Counter> _counters;

    public MetricSet(StatsTimer timer)
    {
        ArgumentNullException.ThrowIfNull(timer);
        Timer = timer;
        _counters = Enum.GetValues<StatId>().ToDictionary(id => id, _ => new Counter());
    }

    public StatsTimer Timer { get; }

    public void Add(StatId stat, decimal amount) => _counters[stat].Add(amount);

    public decimal Total(StatId stat) => _counters[stat].Total;

    public MetricSnapshot Snapshot(StatId stat) => _counters[stat].Snapshot(Timer.Elapsed);

    /// <summary>Re-marks one counter's window; every other window is untouched.</summary>
    public void Reset(StatId stat) => _counters[stat].Mark(Timer.Elapsed);

    public void ResetAll()
    {
        TimeSpan elapsed = Timer.Elapsed;
        foreach (Counter counter in _counters.Values)
        {
            counter.Mark(elapsed);
        }
    }
}
