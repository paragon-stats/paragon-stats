using ParagonStats.Core.Sessions;

namespace ParagonStats.Core.Tui;

/// <summary>
/// An immutable view of the tracker at one instant, which every screen renders
/// from. It exists because neither live collection can be bound directly:
/// <see cref="SessionTracker.Open"/> is an unordered view over a dictionary that
/// mutates while it is read, and <see cref="SessionTracker.Sessions"/>
/// re-allocates and re-sorts on every access. So a frame takes its own copy,
/// once, in the tracker's own total order.
/// <para>
/// Screens render; they do not compute. Anything a screen would otherwise work
/// out for itself belongs here or in a column descriptor.
/// </para>
/// </summary>
public sealed class Snapshot
{
    private Snapshot(IReadOnlyList<SessionRow> rows, SessionRow combined, long unattributed)
    {
        Rows = rows;
        Combined = combined;
        Unattributed = unattributed;
    }

    public IReadOnlyList<SessionRow> Rows { get; }

    /// <summary>
    /// The all-boxes total. Counters sum, but the clock is the span from the
    /// earliest start to the latest activity rather than the sum of the rows:
    /// multiboxing is pervasive here (84 of 90 same-date cross-account file
    /// pairs overlap), so adding per-box spans would report more elapsed time
    /// than actually passed.
    /// </summary>
    public SessionRow Combined { get; }

    public long Unattributed { get; }

    public bool IsEmpty => Rows.Count == 0;

    /// <summary>Builds a frame's worth of state from the live tracker.</summary>
    public static Snapshot Capture(SessionTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return Capture(tracker.Open, tracker.UnattributedCount);
    }

    /// <summary>The seam the tests drive, and what <see cref="Capture(SessionTracker)"/> delegates to.</summary>
    public static Snapshot Capture(IReadOnlyCollection<CharacterSession> sessions, long unattributed)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        // Copy before sorting: the source is a live dictionary view.
        List<CharacterSession> ordered = [.. sessions];
        ordered.Sort(SessionTracker.Compare);

        List<SessionRow> rows = new(ordered.Count);
        foreach (CharacterSession session in ordered)
        {
            rows.Add(RowFor(session));
        }

        return new Snapshot(rows, Total(ordered, rows), unattributed);
    }

    private static SessionRow RowFor(CharacterSession session) => new(
        session.Character,
        session.Account,
        Span(session.Start, session.LastSeen),
        session.Stats.Experience,
        session.Stats.Influence,
        session.Stats.Tickets,
        session.Stats.Defeats,
        session.Stats.Activations,
        session.Stats.TotalDamage,
        session.Stats.MarketIncome,
        session.Stats.MarketSpent);

    private static SessionRow Total(List<CharacterSession> sessions, List<SessionRow> rows)
    {
        if (sessions.Count == 0)
        {
            return new SessionRow("ALL BOXES", string.Empty, TimeSpan.Zero, 0, 0, 0, 0, 0, 0m, 0, 0);
        }

        // Start is the first key of the tracker's total order, so the earliest
        // start is already at the head - scanning for a minimum would be dead
        // code. LastSeen is not a sort key, so the latest still has to be found.
        DateTime first = sessions[0].Start;
        DateTime last = sessions[0].LastSeen;
        foreach (CharacterSession session in sessions)
        {
            if (session.LastSeen > last)
            {
                last = session.LastSeen;
            }
        }

        return new SessionRow(
            "ALL BOXES",
            string.Empty,
            Span(first, last),
            rows.Sum(row => row.Experience),
            rows.Sum(row => row.Influence),
            rows.Sum(row => row.Tickets),
            rows.Sum(row => row.Defeats),
            rows.Sum(row => row.Activations),
            rows.Sum(row => row.Damage),
            rows.Sum(row => row.MarketIncome),
            rows.Sum(row => row.MarketSpent));
    }

    /// <summary>
    /// Clamped in one place. The batch summary clamps for naive local
    /// timestamps running backwards over a DST change; the live line did not,
    /// and every surface reading from here inherits the same answer.
    /// </summary>
    private static TimeSpan Span(DateTime start, DateTime lastSeen)
    {
        TimeSpan span = lastSeen - start;
        return span < TimeSpan.Zero ? TimeSpan.Zero : span;
    }
}
