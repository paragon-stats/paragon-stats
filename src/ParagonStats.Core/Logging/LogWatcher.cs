using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Logging;

/// <summary>
/// Discovers chatlog files under the accounts root on every poll and tails
/// each: new files (daily rollover, first login of a new account) attach as
/// they appear and are read from the start so session context - the banner -
/// is never missed. A file that cannot be opened is retried next poll, never
/// fatal. Files never detach; account = the directory above Logs.
/// </summary>
public sealed class LogWatcher : IDisposable
{
    private readonly string _root;
    private readonly SortedDictionary<string, ChatLogTailer> _tailers = new(StringComparer.Ordinal);

    public LogWatcher(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
    }

    public IReadOnlyList<WatchBatch> Poll()
    {
        foreach (string file in Directory.EnumerateFiles(_root, "chatlog*.txt", SearchOption.AllDirectories))
        {
            if (!_tailers.ContainsKey(file))
            {
                try
                {
                    _tailers.Add(file, new ChatLogTailer(file));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Locked, denied, or vanished mid-attach: retry next poll.
                }
            }
        }

        List<WatchBatch> batches = [];
        foreach ((string file, ChatLogTailer tailer) in _tailers)
        {
            IReadOnlyList<string> lines = tailer.Poll();
            if (lines.Count > 0)
            {
                batches.Add(new WatchBatch(LogReplayer.AccountFor(file), lines));
            }
        }

        return batches;
    }

    public void Dispose()
    {
        foreach (ChatLogTailer tailer in _tailers.Values)
        {
            tailer.Dispose();
        }

        _tailers.Clear();
    }
}
