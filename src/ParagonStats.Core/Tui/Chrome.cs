namespace ParagonStats.Core.Tui;

/// <summary>
/// The frame every screen shares: a header naming the build and the directory
/// in use, and a footer of key hints. Written once so a screen cannot ship
/// without appearing in the footer, and so the layout cannot drift between
/// screens.
/// </summary>
internal static class Chrome
{
    /// <summary>Row the body may start on, below the header and its rule.</summary>
    public const int BodyTop = 3;

    /// <summary>Rows the footer and its rule occupy at the bottom.</summary>
    public const int FooterRows = 2;

    public static void Header(Frame frame, Readout readout, string title)
    {
        frame.Write(0, 1, $"paragon-stats {readout.Version}");
        frame.Write(0, 24, title);

        // Right-aligned so the read-only promise sits in the same place on
        // every screen; the root gives way first when the frame is narrow.
        const string Promise = "read-only *";
        frame.Write(0, frame.Width - Promise.Length - 1, Promise);

        string count = readout.Snapshot.IsEmpty
            ? "no live sessions"
            : $"{readout.Snapshot.Rows.Count} live";
        frame.Write(1, 1, $"{readout.Root}   {count}   unattributed {readout.Snapshot.Unattributed}");
        frame.Rule(2);
    }

    public static void Footer(Frame frame, string hints)
    {
        frame.Rule(frame.Height - FooterRows);
        frame.Write(frame.Height - 1, 1, hints);
    }

    /// <summary>Lays a row of cells out under the column widths, padding or clipping each.</summary>
    public static string Line(IReadOnlyList<Column> columns, Func<Column, string> cell)
    {
        List<string> parts = new(columns.Count);
        foreach (Column column in columns)
        {
            string text = cell(column);
            if (text.Length > column.Width)
            {
                text = text[..column.Width];
            }

            parts.Add(column.RightAligned ? text.PadLeft(column.Width) : text.PadRight(column.Width));
        }

        return string.Join(' ', parts).TrimEnd();
    }
}
