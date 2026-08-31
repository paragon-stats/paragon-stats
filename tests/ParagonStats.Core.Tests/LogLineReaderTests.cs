using ParagonStats.Core.Logging;

namespace ParagonStats.Core.Tests;

public sealed class LogLineReaderTests
{
    [Fact]
    public void Valid_line_parses_timestamp_and_payload()
    {
        Assert.True(LogLineReader.TryParse("2024-05-12 08:16:49 Welcome to City of Heroes, Nova - PRIME!", out LogLine line));
        Assert.Equal(new DateTime(2024, 5, 12, 8, 16, 49), line.Timestamp);
        Assert.Equal(DateTimeKind.Unspecified, line.Timestamp.Kind);
        Assert.Equal("Welcome to City of Heroes, Nova - PRIME!", line.Payload);
    }

    [Fact]
    public void Trailing_carriage_return_is_stripped()
    {
        Assert.True(LogLineReader.TryParse("2024-05-12 08:16:49 payload\r", out LogLine line));
        Assert.Equal("payload", line.Payload);
    }

    [Theory]
    [InlineData("| redacted continuation line kept for format")]
    [InlineData("")]
    [InlineData("not a timestamp at all, just words")]
    [InlineData("2024-13-99 08:16:49 impossible date")]
    [InlineData("2024-05-12 08:16:49")]
    public void Non_matching_lines_are_skipped_not_thrown(string raw)
    {
        Assert.False(LogLineReader.TryParse(raw, out _));
    }
}
