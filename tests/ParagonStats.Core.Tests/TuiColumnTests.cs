using ParagonStats.Core.Tui;

namespace ParagonStats.Core.Tests;

/// <summary>
/// The column registry is the seam CP2's metrics plug into, and the only place
/// a cell is formatted. Screens render; they do not compute - so the formatting
/// rules are pinned here.
/// </summary>
public sealed class TuiColumnTests
{
    [Fact]
    public void The_default_readout_fits_the_strip()
    {
        // 120x12 is the shipped frame. A readout wider than the frame would be
        // silently clipped, losing the right-hand metrics without saying so.
        Assert.True(
            Columns.TotalWidth(Columns.Default) <= 120,
            $"default columns need {Columns.TotalWidth(Columns.Default)} of 120 columns");
    }

    [Fact]
    public void Total_width_counts_the_gaps_between_columns()
    {
        List<Column> columns = [new("A", 3, static row => row.Character), new("B", 4, static row => row.Account)];

        Assert.Equal(8, Columns.TotalWidth(columns)); // 3 + 1 + 4
        Assert.Equal(0, Columns.TotalWidth([]));
        Assert.Throws<ArgumentNullException>(() => Columns.TotalWidth(null!));
    }

    [Fact]
    public void A_narrow_window_sheds_the_least_important_columns_first()
    {
        // Default is ordered by how much a farmer needs it, so the per-hour
        // rates go before the character name ever does.
        IReadOnlyList<Column> narrow = Columns.Fit(Columns.Default, 60);

        Assert.True(Columns.TotalWidth(narrow) <= 60);
        Assert.Equal("CHARACTER", narrow[0].Header, StringComparer.Ordinal);
        Assert.DoesNotContain(narrow, column => string.Equals(column.Header, "INF/hr", StringComparison.Ordinal));
    }

    [Fact]
    public void Fitting_a_wide_window_keeps_everything()
    {
        Assert.Same(Columns.Default, Columns.Fit(Columns.Default, 500));
        Assert.Equal(Columns.Default.Count, Columns.Fit(Columns.Default, Columns.TotalWidth(Columns.Default)).Count);
    }

    [Fact]
    public void An_impossibly_narrow_window_still_keeps_one_column()
    {
        // A readout with no columns is not a narrower readout, it is a blank screen.
        IReadOnlyList<Column> fitted = Columns.Fit(Columns.Default, 1);

        Assert.Single(fitted);
        Assert.Equal("CHARACTER", fitted[0].Header, StringComparer.Ordinal);
    }

    [Fact]
    public void Fitting_handles_the_empty_and_null_cases()
    {
        Assert.Empty(Columns.Fit([], 80));
        Assert.Throws<ArgumentNullException>(() => Columns.Fit(null!, 80));
    }

    [Fact]
    public void Every_default_column_renders_a_cell()
    {
        // Deliberately round: over exactly two hours, 1,200,000 xp is 600,000
        // an hour and 4,000,000 inf is 2,000,000 an hour. Every expected value
        // here is checkable by eye rather than by rerunning the formatter.
        SessionRow row = Row(TimeSpan.FromHours(2));

        List<string> cells = [.. Columns.Default.Select(column => column.Value(row))];

        Assert.Equal(
            ["Nova", "acct", "02:00:00", "1,200,000", "4,000,000", "300", "600,000", "2,000,000"],
            cells,
            StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(0, 0, 0, "00:00:00")]
    [InlineData(2, 41, 2, "02:41:02")]
    [InlineData(9, 5, 30, "09:05:30")]

    // A 30-hour session reads 30:00:00, not 06:00:00 on an invisible second day.
    [InlineData(30, 0, 0, "30:00:00")]
    [InlineData(100, 0, 0, "100:00:00")]
    public void The_clock_reports_total_hours(int hours, int minutes, int seconds, string expected)
    {
        Column clock = Columns.Default.Single(column => string.Equals(column.Header, "CLOCK", StringComparison.Ordinal));

        Assert.Equal(expected, clock.Value(Row(new TimeSpan(hours, minutes, seconds))), StringComparer.Ordinal);
    }

    [Fact]
    public void A_zero_span_reports_zero_rates_rather_than_infinity()
    {
        Column rate = Columns.Default.Single(column => string.Equals(column.Header, "XP/hr", StringComparison.Ordinal));

        Assert.Equal("0", rate.Value(Row(TimeSpan.Zero)));
    }

    [Fact]
    public void Names_stay_left_aligned_and_numbers_go_right()
    {
        Assert.False(Columns.Default[0].RightAligned); // CHARACTER
        Assert.False(Columns.Default[1].RightAligned); // ACCOUNT
        Assert.All(Columns.Default.Skip(2), column => Assert.True(column.RightAligned));
    }

    [Fact]
    public void Headers_are_printable_ascii_and_fit_their_own_width()
    {
        foreach (Column column in Columns.Default)
        {
            Assert.True(column.Header.Length <= column.Width, $"{column.Header} does not fit its own column");
            Assert.All(column.Header, symbol => Assert.True(symbol >= ' ' && symbol <= '~', "non-printable header"));
        }
    }

    private static SessionRow Row(TimeSpan clock) =>
        new("Nova", "acct", clock, 1_200_000, 4_000_000, 300, 7, 940, 12_345.6m, 500, 200);
}
