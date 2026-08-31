using System.Globalization;

namespace ParagonStats.Core.Logging;

/// <summary>
/// Turns raw text lines into <see cref="LogLine"/>s. Homecoming writes
/// "yyyy-MM-dd HH:mm:ss " + payload; anything else (MOTD continuation lines,
/// garbage) is skipped by returning false - the reader never throws.
/// </summary>
public static class LogLineReader
{
    private const int TimestampLength = 19; // "yyyy-MM-dd HH:mm:ss"

    public static bool TryParse(string raw, out LogLine line)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Length > 0 && raw[^1] == '\r')
        {
            raw = raw[..^1];
        }

        if (raw.Length < TimestampLength + 2 || raw[TimestampLength] != ' ')
        {
            line = default;
            return false;
        }

        if (!DateTime.TryParseExact(
                raw.AsSpan(0, TimestampLength),
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime timestamp))
        {
            line = default;
            return false;
        }

        line = new LogLine(timestamp, raw[(TimestampLength + 1)..]);
        return true;
    }
}
