namespace ParagonStats.Core.Logging;

/// <summary>
/// Discovers chatlog files under the accounts root and tails each. Watch is a
/// LIVE monitor: only files written within the attach window join (the
/// caller passes the session idle timeout - a file silent past it belongs to
/// a closed session by definition; history stays on disk for batch replay),
/// read from the start so session
/// context - the banner - is never missed. New files (daily rollover, first
/// login of a new account) attach as they appear; a file that cannot be
/// opened is reported via <see cref="Unreadable"/> and retried, never fatal;
/// a transient read failure skips that file for one poll. Files never detach;
/// account = the directory above Logs.
/// </summary>
public sealed class LogWatcher : IDisposable
{
    private readonly string _root;
    private readonly TimeSpan _attachWindow;
    private readonly int _discoveryInterval;
    private readonly SortedDictionary<string, ChatLogTailer> _tailers = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _unreadable = new(StringComparer.Ordinal);
    private int _pollsSinceDiscovery;

    /// <summary>
    /// Discovery walks the whole tree; new files appear about once per
    /// account per day, so rediscovering every Nth poll (default: roughly
    /// every 10s at the CLI's 500ms cadence) spares two tree walks per second.
    /// </summary>
    public LogWatcher(string root, TimeSpan attachWindow, int discoveryInterval = 20)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfLessThan(discoveryInterval, 1);
        _root = root;
        _attachWindow = attachWindow;
        _discoveryInterval = discoveryInterval;
    }

    /// <summary>Files that repeatedly fail to open; surfaced in the final summary like batch skips.</summary>
    public IReadOnlyCollection<string> Unreadable => _unreadable;

    public IReadOnlyList<WatchBatch> Poll()
    {
        if (_pollsSinceDiscovery == 0)
        {
            Discover();
        }

        _pollsSinceDiscovery = (_pollsSinceDiscovery + 1) % _discoveryInterval;

        List<WatchBatch> batches = [];
        foreach ((string file, ChatLogTailer tailer) in _tailers)
        {
            IReadOnlyList<string> lines;
            try
            {
                lines = tailer.Poll();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue; // transient (disk, AV, share change): retry next poll
            }

            if (lines.Count > 0)
            {
                batches.Add(new WatchBatch(ChatLogTree.AccountFor(file), lines));
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

    private void Discover()
    {
        DateTime cutoff = DateTime.UtcNow - _attachWindow;
        foreach (string file in Directory.EnumerateFiles(_root, ChatLogTree.FilePattern, ChatLogTree.SafeRecurse))
        {
            if (_tailers.ContainsKey(file) || File.GetLastWriteTimeUtc(file) < cutoff)
            {
                continue;
            }

            try
            {
                _tailers.Add(file, new ChatLogTailer(file));
                _unreadable.Remove(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _unreadable.Add(file); // locked or denied: reported, retried next discovery
            }
        }
    }
}
