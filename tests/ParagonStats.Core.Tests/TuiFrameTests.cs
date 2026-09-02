using ParagonStats.Core.Tui;

namespace ParagonStats.Core.Tests;

/// <summary>
/// The frame is the one place TUI text passes through, so the ASCII guarantee
/// and the plain/ANSI equivalence are enforced here rather than trusted to
/// every screen (Core sits outside check-encoding's scanned roots).
/// </summary>
public sealed class TuiFrameTests
{
    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 4)]
    [InlineData(4, -1)]
    public void A_frame_needs_positive_dimensions(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Frame(width, height));

    [Fact]
    public void A_new_frame_is_blank()
    {
        Frame frame = new(6, 2);

        Assert.Equal(6, frame.Width);
        Assert.Equal(2, frame.Height);
        Assert.Equal("\n", frame.ToPlainText()); // two rows, both trimmed to nothing
    }

    [Fact]
    public void Text_lands_where_it_is_written()
    {
        Frame frame = new(12, 3);

        frame.Write(0, 0, "top");
        frame.Write(2, 4, "down");

        Assert.Equal("top\n\n    down", frame.ToPlainText());
    }

    [Fact]
    public void Text_is_clipped_at_the_right_edge()
    {
        Frame frame = new(8, 1);

        frame.Write(0, 4, "overflowing");

        Assert.Equal("    over", frame.ToPlainText());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 8)]
    public void Off_frame_writes_are_ignored_rather_than_thrown(int row, int column)
    {
        // A layout bug should render wrong, not kill the process mid-session.
        Frame frame = new(8, 3);

        frame.Write(row, column, "lost");

        Assert.Equal("\n\n", frame.ToPlainText());
    }

    [Fact]
    public void A_negative_column_writes_the_visible_tail()
    {
        Frame frame = new(6, 1);

        frame.Write(0, -2, "abcdef");

        Assert.Equal("cdef", frame.ToPlainText());
    }

    [Fact]
    public void Non_printable_characters_never_reach_the_buffer()
    {
        Frame frame = new(10, 1);

        // Built from code points so the assertion cannot change meaning if this
        // file's own encoding is ever rewritten: tab, e-acute, box-drawing.
        string hostile = string.Concat("a\tb", (char)233, "c", (char)9472, "d");

        frame.Write(0, 0, hostile);

        Assert.Equal("a?b?c?d", frame.ToPlainText());
        Assert.All(frame.ToPlainText(), symbol =>
            Assert.True(symbol is '\n' || (symbol >= ' ' && symbol <= '~'), "non-printable in frame"));
    }

    [Fact]
    public void A_rule_fills_its_row()
    {
        Frame frame = new(5, 3);

        frame.Rule(1);

        Assert.Equal("\n-----\n", frame.ToPlainText());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void An_off_frame_rule_is_ignored(int row)
    {
        Frame frame = new(5, 3);

        frame.Rule(row);

        Assert.Equal("\n\n", frame.ToPlainText());
    }

    [Fact]
    public void A_null_write_is_rejected() =>
        Assert.Throws<ArgumentNullException>(() => new Frame(4, 1).Write(0, 0, null!));

    [Fact]
    public void The_ansi_render_carries_the_same_text_as_the_plain_one()
    {
        Frame frame = new(10, 2);
        frame.Write(0, 0, "header");
        frame.Rule(1);

        string ansi = frame.ToAnsi();

        // Same content, different envelope: strip the escapes and it is the plain render.
        Assert.Equal(frame.ToPlainText(), Strip(ansi));
    }

    [Fact]
    public void The_ansi_render_homes_and_erases_so_repaints_do_not_scroll()
    {
        Frame frame = new(4, 2);
        frame.Write(0, 0, "hi");

        string ansi = frame.ToAnsi();
        string escape = ((char)27).ToString();

        Assert.StartsWith(escape + "[H", ansi, StringComparison.Ordinal);
        Assert.EndsWith(escape + "[J", ansi, StringComparison.Ordinal);
        Assert.Equal(2, Occurrences(ansi, escape + "[K")); // one per row
    }

    private static string Strip(string ansi)
    {
        string escape = ((char)27).ToString();
        return ansi
            .Replace(escape + "[H", string.Empty, StringComparison.Ordinal)
            .Replace(escape + "[K", string.Empty, StringComparison.Ordinal)
            .Replace(escape + "[J", string.Empty, StringComparison.Ordinal);
    }

    private static int Occurrences(string text, string needle)
    {
        int found = 0;
        for (int index = text.IndexOf(needle, StringComparison.Ordinal); index >= 0;
             index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            found++;
        }

        return found;
    }
}
