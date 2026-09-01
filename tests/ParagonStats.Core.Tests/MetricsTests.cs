using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

public sealed class MetricsTests
{
    private readonly FakeTimeProvider _clock = new();

    [Fact]
    public void Snapshot_rates_derive_from_value_over_window()
    {
        MetricSnapshot s = MetricSnapshot.Compute(100m, TimeSpan.FromMinutes(2));
        Assert.Equal(100m, s.Value);
        Assert.Equal(50m, s.PerMinute);
        Assert.Equal(3000m, s.PerHour);
    }

    [Fact]
    public void Zero_or_negative_window_yields_zero_rates_never_infinity()
    {
        Assert.Equal(0m, MetricSnapshot.Compute(100m, TimeSpan.Zero).PerMinute);
        Assert.Equal(0m, MetricSnapshot.Compute(100m, TimeSpan.FromSeconds(-5)).PerHour);
    }

    [Fact]
    public void Timer_accumulates_only_while_running()
    {
        StatsTimer timer = new(_clock);
        _clock.Advance(TimeSpan.FromMinutes(1)); // before Start: not counted
        timer.Start();
        _clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(TimeSpan.FromMinutes(2), timer.Elapsed);
    }

    [Fact]
    public void Pause_freezes_elapsed_and_resume_continues()
    {
        StatsTimer timer = new(_clock);
        timer.Start();
        _clock.Advance(TimeSpan.FromMinutes(3));
        timer.Pause();
        _clock.Advance(TimeSpan.FromMinutes(10)); // frozen: rates' denominator must not grow
        Assert.Equal(TimeSpan.FromMinutes(3), timer.Elapsed);
        timer.Resume();
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromMinutes(4), timer.Elapsed);
    }

    [Fact]
    public void Redundant_pause_resume_and_start_are_idempotent()
    {
        StatsTimer timer = new(_clock);
        timer.Resume();                       // not started: no-op
        timer.Pause();                        // not started: no-op
        Assert.Equal(TimeSpan.Zero, timer.Elapsed);
        timer.Start();
        _clock.Advance(TimeSpan.FromMinutes(1));
        timer.Start();                        // already running: no-op
        timer.Resume();                       // already running: no-op
        _clock.Advance(TimeSpan.FromMinutes(1));
        timer.Pause();
        timer.Pause();                        // already paused: no-op
        Assert.Equal(TimeSpan.FromMinutes(2), timer.Elapsed);
    }

    [Fact]
    public void Timer_reset_zeroes_the_window()
    {
        StatsTimer timer = new(_clock);
        timer.Start();
        _clock.Advance(TimeSpan.FromMinutes(5));
        timer.Reset();
        Assert.Equal(TimeSpan.Zero, timer.Elapsed);
        _clock.Advance(TimeSpan.FromMinutes(1)); // still running after reset
        Assert.Equal(TimeSpan.FromMinutes(1), timer.Elapsed);
    }

    [Fact]
    public void Metric_set_snapshots_each_stat_uniformly()
    {
        MetricSet metrics = new(new StatsTimer(_clock));
        metrics.Timer.Start();
        metrics.Add(StatId.Experience, 600);
        metrics.Add(StatId.Damage, 90);
        _clock.Advance(TimeSpan.FromMinutes(3));

        Assert.Equal(200m, metrics.Snapshot(StatId.Experience).PerMinute);
        Assert.Equal(30m, metrics.Snapshot(StatId.Damage).PerMinute);
        Assert.Equal(0m, metrics.Snapshot(StatId.Defeats).Value);
    }

    [Fact]
    public void Selective_reset_moves_one_window_and_leaves_the_rest()
    {
        MetricSet metrics = new(new StatsTimer(_clock));
        metrics.Timer.Start();
        metrics.Add(StatId.Experience, 100);
        metrics.Add(StatId.Defeats, 4);
        _clock.Advance(TimeSpan.FromMinutes(2));

        metrics.Reset(StatId.Experience);
        _clock.Advance(TimeSpan.FromMinutes(2));
        metrics.Add(StatId.Experience, 30);

        MetricSnapshot xp = metrics.Snapshot(StatId.Experience);
        Assert.Equal(30m, xp.Value);           // window value: pre-reset 100 excluded
        Assert.Equal(15m, xp.PerMinute);       // 30 over the 2 minutes since ITS mark
        Assert.Equal(1m, metrics.Snapshot(StatId.Defeats).PerMinute); // 4 over 4 min: untouched
        Assert.Equal(130m, metrics.Total(StatId.Experience));         // lifetime survives reset
    }

    [Fact]
    public void Reset_all_re_marks_every_counter()
    {
        MetricSet metrics = new(new StatsTimer(_clock));
        metrics.Timer.Start();
        metrics.Add(StatId.Influence, 1000);
        metrics.Add(StatId.Activations, 10);
        _clock.Advance(TimeSpan.FromMinutes(5));

        metrics.ResetAll();
        Assert.Equal(0m, metrics.Snapshot(StatId.Influence).Value);
        Assert.Equal(0m, metrics.Snapshot(StatId.Activations).Value);
        Assert.Equal(1000m, metrics.Total(StatId.Influence));
    }

    [Fact]
    public void Paused_timer_freezes_rates()
    {
        MetricSet metrics = new(new StatsTimer(_clock));
        metrics.Timer.Start();
        metrics.Add(StatId.Damage, 120);
        _clock.Advance(TimeSpan.FromMinutes(2));
        metrics.Timer.Pause();
        _clock.Advance(TimeSpan.FromMinutes(58)); // an hour AFK: rate must not decay

        Assert.Equal(60m, metrics.Snapshot(StatId.Damage).PerMinute);
    }
}
