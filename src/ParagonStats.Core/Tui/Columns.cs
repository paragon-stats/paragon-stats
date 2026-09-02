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

        int taken = 0;
        int used = 0;
        foreach (Column column in columns)
        {
            int next = used == 0 ? column.Width : used + 1 + column.Width;
            if (taken > 0 && next > width)
            {
                break;
            }

            used = next;
            taken++;
        }

        // Always at least one column: a readout with no columns is not a
        // narrower readout, it is a blank screen.
        return taken == columns.Count ? columns : [.. columns.Take(Math.Max(1, taken))];
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
