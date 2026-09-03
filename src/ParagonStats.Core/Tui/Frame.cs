using System.Text;

namespace ParagonStats.Core.Tui;

/// <summary>
/// A fixed-size character buffer that screens draw into, rendered either as
/// ANSI (a repainting terminal) or as plain text (redirected output, CI
/// goldens, and any host without virtual-terminal support). Both renderers
/// read the same buffer, so a screen cannot say one thing on a terminal and
/// another in a pipe - the plain path is the same content through a different
/// writer, not a fallback that drifts.
/// </summary>
public sealed class Frame
{
    /// <summary>
    /// ESC by code point, not by escape sequence and not by a literal control
    /// byte pasted into the file. Both alternatives have already gone wrong
    /// here once: a raw byte makes the source non-ASCII, and an escape
    /// sequence survives only until something in the editing chain decodes it.
    /// 27 cannot be silently rewritten.
    /// </summary>
    private const char Escape = (char)27;

    private readonly char[] _cells;

    public Frame(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        Width = width;
        Height = height;
        _cells = new char[width * height];
        Array.Fill(_cells, ' ');
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>
    /// Draws text at a position, clipped to the frame. Off-frame positions are
    /// ignored rather than thrown: a layout bug should render wrong, not take
    /// the process down mid-session while someone is playing.
    /// </summary>
    public void Write(int row, int column, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (row < 0 || row >= Height || column >= Width)
        {
            return;
        }

        int start = Math.Max(column, 0);
        int skipped = start - column;
        for (int index = skipped; index < text.Length && start + index - skipped < Width; index++)
        {
            _cells[(row * Width) + start + index - skipped] = Printable(text[index]);
        }
    }

    /// <summary>Fills a row with a horizontal rule.</summary>
    public void Rule(int row)
    {
        if (row < 0 || row >= Height)
        {
            return;
        }

        Array.Fill(_cells, '-', row * Width, Width);
    }

    /// <summary>
    /// The buffer as plain lines, trailing blanks trimmed so goldens do not pin
    /// invisible whitespace.
    /// </summary>
    public string ToPlainText()
    {
        StringBuilder builder = new();
        for (int row = 0; row < Height; row++)
        {
            if (row > 0)
            {
                builder.Append('\n');
            }

            builder.Append(RowText(row).TrimEnd());
        }

        return builder.ToString();
    }

    /// <summary>
    /// The buffer as one repaint: home the cursor, write every row and erase to
    /// its end as it goes, then clear everything the previous frame left below.
    /// </summary>
    public string ToAnsi()
    {
        StringBuilder builder = new();
        builder.Append(Escape).Append("[H");
        for (int row = 0; row < Height; row++)
        {
            builder.Append(RowText(row).TrimEnd()).Append(Escape).Append("[K");
            if (row < Height - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.Append(Escape).Append("[J").ToString();
    }

    /// <summary>
    /// Console output stays printable ASCII (docs/style-guides/encoding.md).
    /// Enforced here rather than trusted to every screen, because Core sits
    /// outside check-encoding's scanned roots - a box-drawing character would
    /// otherwise pass the hook and CI and surface as mojibake on a legacy code
    /// page.
    /// </summary>
    private static char Printable(char symbol) => symbol is >= ' ' and <= '~' ? symbol : '?';

    private string RowText(int row) => new(_cells, row * Width, Width);
}
