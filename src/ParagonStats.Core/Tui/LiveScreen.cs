namespace ParagonStats.Core.Tui;

/// <summary>
/// The destination screen and the point of the checkpoint: a live per-character
/// readout with an all-boxes total, repainting on a cadence so the clock
/// advances even when no log line arrived.
/// </summary>
public sealed class LiveScreen : IScreen
{
    private readonly IReadOnlyList<Column> _columns;

    public LiveScreen()
        : this(Columns.Default)
    {
    }

    public LiveScreen(IReadOnlyList<Column> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns;
    }

    public string Title => "live";

    public string Hints => "[m] menu   [h] help   [q] quit";

    public void Render(Frame frame, Readout readout)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(readout);

        Chrome.Header(frame, readout, Title);
        frame.Write(Chrome.BodyTop, 1, Chrome.Line(_columns, static column => column.Header));

        int firstRow = Chrome.BodyTop + 1;
        int lastRow = frame.Height - Chrome.FooterRows - 1;
        if (readout.Snapshot.IsEmpty)
        {
            // Say why the table is empty. A blank body reads as broken, which
            // is the failure this whole checkpoint exists to correct.
            frame.Write(firstRow, 1, "Waiting for a character to log in. Chat logging must be on:");
            frame.Write(firstRow + 1, 1, "Options > Windows > Chat > Log Chat.");
            Chrome.Footer(frame, Hints);
            return;
        }

        // Reserve the total's rule and row up front. Drawing them after the
        // sessions and hoping they fit overwrote the overflow notice, which
        // silently under-reported the farm - the one thing this must not do.
        bool showCombined = readout.Snapshot.Rows.Count > 1;
        int capacity = Math.Max(1, lastRow - firstRow + 1 - (showCombined ? 2 : 0));
        bool overflow = readout.Snapshot.Rows.Count > capacity;
        int shown = overflow ? capacity - 1 : readout.Snapshot.Rows.Count;

        int row = firstRow;
        foreach (SessionRow session in readout.Snapshot.Rows.Take(shown))
        {
            frame.Write(row, 1, Chrome.Line(_columns, column => column.Value(session)));
            row++;
        }

        if (overflow)
        {
            frame.Write(row, 1, $"... {readout.Snapshot.Rows.Count - shown} more (widen or lengthen the window)");
            row++;
        }

        if (showCombined)
        {
            frame.Rule(row);
            frame.Write(row + 1, 1, Chrome.Line(_columns, column => column.Value(readout.Snapshot.Combined)));
        }

        Chrome.Footer(frame, Hints);
    }

    public ScreenResult Key(char pressed) => pressed switch
    {
        'q' or 'Q' => ScreenResult.Quit,
        'm' or 'M' => ScreenResult.Menu,
        'h' or 'H' => ScreenResult.Help,
        _ => ScreenResult.Stay,
    };
}
