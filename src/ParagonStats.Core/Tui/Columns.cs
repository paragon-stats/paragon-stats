using System.Globalization;

using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tui;

/// <summary>
/// The default readout, sized for the 120x12 strip. Wall-clock counts and
/// averages by operator ruling; active play is a future alternative, so nothing
/// here divides by anything but elapsed time.
/// </summary>
public static class Columns
{
    public static IReadOnlyList<Column> Default { get; } =
    [
        new("CHARACTER", 18, static row => row.Character),
        new("ACCOUNT", 10, static row => row.Account),
        new("CLOCK", 8, static row => Elapsed(row.Clock), RightAligned: true),
        new("XP", 11, static row => Count(row.Experience), RightAligned: true),
        new("INF", 11, static row => Count(row.Influence), RightAligned: true),
        new("TICKETS", 7, static row => Count(row.Tickets), RightAligned: true),
        new("XP/hr", 10, static row => Rate(row.Experience, row.Clock), RightAligned: true),
        new("INF/hr", 10, static row => Rate(row.Influence, row.Clock), RightAligned: true),
    ];

    /// <summary>
    /// The longest run of columns that fits the given width. `Default` is
    /// ordered by how much a farmer needs it, so a narrow window sheds the
    /// per-hour rates first and never the character name. Fitting beats
    /// hand-tuning a layout for one terminal size and then discovering the
    /// right-hand columns are invisible on someone else's font.
    /// </summary>
    public static IReadOnlyList<Column> Fit(IReadOnlyList<Column> columns, int width)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
        {
            return columns;
        }

        // Rendered width after each column: its own width, plus one space for the
        // gap back to the previous one. A scan rather than a projection - each
        // entry depends on the one before it - so it is an Aggregate, and the
        // "is this the first column" test is positional. Keying that test on the
        // running total instead let a zero-width column pose as the first one,
        // so the separator after it went uncharged and the readout overran its
        // frame. Eight columns twice a second: the list costs nothing.
        List<int> rendered = columns.Aggregate(
            new List<int>(columns.Count),
            (running, column) =>
            {
                running.Add(running.Count == 0 ? column.Width : running[^1] + 1 + column.Width);
                return running;
            });

        // The first column is taken unconditionally: a readout with no columns is
        // not a narrower readout, it is a blank screen.
        int taken = rendered.TakeWhile((total, index) => index == 0 || total <= width).Count();

        return taken == columns.Count ? columns : [.. columns.Take(taken)];
    }

    /// <summary>Width of the whole readout, counting the single space between columns.</summary>
    public static int TotalWidth(IReadOnlyList<Column> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        int total = columns.Sum(column => column.Width + 1);
        return total == 0 ? 0 : total - 1;
    }

    /// <summary>
    /// Hours do not roll over into days: a session that ran 30 hours reads
    /// 30:00:00, not 06:00:00 on an invisible second day.
    /// </summary>
    private static string Elapsed(TimeSpan span) => string.Create(
        CultureInfo.InvariantCulture,
        $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}");

    private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Rate(long value, TimeSpan window) => string.Create(
        CultureInfo.InvariantCulture,
        $"{MetricSnapshot.Compute(value, window).PerHour:N0}");
}
