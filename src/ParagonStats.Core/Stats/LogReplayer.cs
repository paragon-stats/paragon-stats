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
    /// An unreadable file is skipped and reported, never fatal - the live
    /// client legitimately holds today's log open.
    /// </summary>
    public static ReplayResult Replay(IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        SessionTracker tracker = new();
        List<string> skipped = [];
        foreach (string file in files)
        {
            try
            {
                ReplayFile(tracker, file);
            }
            catch (IOException)
            {
                skipped.Add(file);
            }
            catch (UnauthorizedAccessException)
            {
                skipped.Add(file);
            }
        }

        return new ReplayResult(tracker.Sessions, tracker.UnattributedCount, skipped);
    }

    /// <summary>
    /// The account is the directory above "Logs" ("accounts\name\Logs\chatlog ....txt").
    /// Files outside that shape key on their own parent directory, so unrelated
    /// locations can never collapse into one shared account.
    /// </summary>
    internal static string AccountFor(string file)
    {
        DirectoryInfo? parent = new FileInfo(file).Directory;
        return parent is not null
            && string.Equals(parent.Name, "Logs", StringComparison.OrdinalIgnoreCase)
            && parent.Parent is not null
            ? parent.Parent.Name
            : parent?.FullName ?? "unknown";
    }

    private static void ReplayFile(SessionTracker tracker, string file)
    {
        string account = AccountFor(file);

        // ReadWrite|Delete sharing: the running game client keeps today's
        // chatlog open for writing, and that must never block a replay.
        using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using StreamReader reader = new(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } raw)
        {
            if (!LogLineReader.TryParse(raw, out LogLine line))
            {
                continue;
            }

            tracker.Accept(account, line, LineParser.Parse(line));
        }
    }
}
