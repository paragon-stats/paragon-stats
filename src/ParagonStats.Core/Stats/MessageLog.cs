using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Stats;

/// <summary>
/// Bounded in-memory capture of parsed lines (#128): the live tier. The log
/// files on disk remain the durable source of truth, so overflow drops the
/// oldest entries; <see cref="TotalCaptured"/> keeps the true count.
/// </summary>
public sealed class MessageLog
{
    public const int Capacity = 10_000;

    private readonly Queue<CapturedMessage> _messages = new();

    public long TotalCaptured { get; private set; }

    public IReadOnlyCollection<CapturedMessage> Messages => _messages;

    public void Add(DateTime timestamp, EventCategory category, string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // Defense in depth: the reader and the parser both refuse
        // communication lines, so nothing should arrive here - but this is
        // the only place a payload is retained for the process lifetime, so
        // it refuses too rather than trusting the gates upstream.
        if (CollectionPolicy.RefusesPayload(payload))
        {
            return;
        }

        if (_messages.Count == Capacity)
        {
            _messages.Dequeue();
        }

        _messages.Enqueue(new CapturedMessage(timestamp, category, payload));
        TotalCaptured++;
    }
}
