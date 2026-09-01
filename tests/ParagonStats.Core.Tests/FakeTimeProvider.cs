namespace ParagonStats.Core.Tests;

/// <summary>Deterministic clock: time moves only when a test advances it.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public void Advance(TimeSpan span) => _ticks += span.Ticks;
}
