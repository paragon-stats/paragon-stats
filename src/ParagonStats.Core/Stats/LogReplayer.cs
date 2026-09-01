using ParagonStats.Core.Logging;
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
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                skipped.Add(file);
            }
        }

        return new ReplayResult(tracker.Sessions, tracker.UnattributedCount, skipped);
    }

    private static void ReplayFile(SessionTracker tracker, string file)
    {
        string account = ChatLogTree.AccountFor(file);

        // The tailer is the ONE reader: same live-writer sharing, and the
        // zero-collection policy applied before any refused line becomes a
        // string (memory-sniffer hardening) - batch and live are identical.
        using ChatLogTailer tailer = new(file);
        for (IReadOnlyList<string> lines = tailer.Poll(); lines.Count > 0; lines = tailer.Poll())
        {
            foreach (string raw in lines)
            {
                tracker.Accept(account, raw);
            }
        }

        // A final line without a trailing newline is still a complete line.
        if (tailer.Drain() is { } tail)
        {
            tracker.Accept(account, tail);
        }
    }
}
