namespace ParagonStats.Core.Stats;

/// <summary>
/// Active-playtime clock for rate windows (#125): accumulates only while
/// running, so pausing freezes every rate's denominator. Built on
/// <see cref="TimeProvider"/> timestamp arithmetic - injectable in tests,
/// monotonic in production, immune to wall-clock jumps.
/// </summary>
public sealed class StatsTimer
{
    private readonly TimeProvider _clock;
    private TimeSpan _accumulated;
    private long _runningSince = -1;

    public StatsTimer(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public TimeSpan Elapsed =>
        _runningSince < 0 ? _accumulated : _accumulated + _clock.GetElapsedTime(_runningSince);

    private bool IsRunning => _runningSince >= 0;

    public void Start()
    {
        if (!IsRunning)
        {
            _runningSince = _clock.GetTimestamp();
        }
    }

    public void Pause()
    {
        if (IsRunning)
        {
            _accumulated += _clock.GetElapsedTime(_runningSince);
            _runningSince = -1;
        }
    }

    public void Resume() => Start();

    /// <summary>Zeroes the window; a running timer keeps running from now.</summary>
    public void Reset()
    {
        _accumulated = TimeSpan.Zero;
        if (IsRunning)
        {
            _runningSince = _clock.GetTimestamp();
        }
    }
}
