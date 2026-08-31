using System.Text;

using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;
using ParagonStats.Core.Sessions;

namespace ParagonStats.Core.Stats;

/// <summary>
/// Batch path and the recalculation primitive (#131): run reader, parser, and
/// fold over whole files. Deterministic - replaying the same files always
/// reproduces the same stats, which is what makes the on-disk logs the source
/// of truth.
/// </summary>
public static class LogReplayer
{
    /// <summary>
    /// Files are processed in the given order; the caller supplies them
    /// chronologically per account (daily chatlog names sort ordinally).
    /// </summary>
    public static ReplayResult Replay(IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        SessionTracker tracker = new();
        foreach (string file in files)
        {
            string account = AccountFor(file);
            foreach (string raw in File.ReadLines(file, Encoding.UTF8))
            {
                if (!LogLineReader.TryParse(raw, out LogLine line))
                {
                    continue;
                }

                tracker.Accept(account, line, LineParser.Parse(line));
            }
        }

        return new ReplayResult(tracker.Sessions, tracker.UnattributedCount);
    }

    /// <summary>The account is the directory above "Logs" ("accounts\name\Logs\chatlog ....txt").</summary>
    internal static string AccountFor(string file)
    {
        DirectoryInfo? logs = new FileInfo(file).Directory;
        if (logs is not null
            && string.Equals(logs.Name, "Logs", StringComparison.OrdinalIgnoreCase)
            && logs.Parent is not null)
        {
            return logs.Parent.Name;
        }

        return "unknown";
    }
}
