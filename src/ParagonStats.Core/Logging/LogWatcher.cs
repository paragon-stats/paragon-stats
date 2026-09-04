namespace ParagonStats.Core.Logging;

/// <summary>
/// Discovers chatlog files under the accounts root and tails each. Watch is a
/// LIVE monitor: only files written within the attach window join (the caller
/// passes the session idle timeout - a file silent past it belongs to a
/// closed session by definition; history stays on disk for batch replay),
/// read from the start so session context - the banner - is never missed.
/// Discovery is bounded on every axis a hostile or merely unusual filesystem
/// could exploit: it skips reparse points (so junction cycles cannot be
/// walked forever), caps recursion depth, ignores inaccessible subtrees,
/// takes only files under a "Logs" directory, and stops attaching past
/// <see cref="MaxTailers"/>. A file that cannot be opened - or that keeps
/// failing after it was opened - is reported via <see cref="Unreadable"/> and
/// retried on a later discovery, never fatal.
/// Account = the directory above Logs.
/// </summary>
public sealed class LogWatcher : IDisposable
{
    /// <summary>Multiboxing is a handful of clients; anything beyond this is a runaway tree, not play.</summary>
    public const int MaxTailers = 64;

    private const int FailuresBeforeDetach = 5;

    private readonly string _root;
    private readonly TimeSpan _attachWindow;
    private readonly int _discoveryInterval;
    private readonly SortedDictionary<string, ChatLogTailer> _tailers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _failures = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _unreadable = new(StringComparer.Ordinal);
    private int _pollsSinceDiscovery;

    /// <summary>
    /// Discovery walks the tree; new files appear about once per account per
    /// day, so rediscovering every Nth poll (default: roughly every 10s at
    /// the CLI's 500ms cadence) spares two tree walks per second.
    /// </summary>
    public LogWatcher(string root, TimeSpan attachWindow, int discoveryInterval = 20)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfLessThan(discoveryInterval, 1);
        _root = root;
        _attachWindow = attachWindow;
        _discoveryInterval = discoveryInterval;
    }

    /// <summary>Files that could not be read; surfaced in the final summary like batch skips.</summary>
    public IReadOnlyCollection<string> Unreadable => _unreadable;

    /// <summary>
    /// How many accounts are CURRENTLY writing log lines - the boxes actually
    /// feeding the readout. Compared against the number of running clients,
    /// this is what lets the tool say a box has gone silent instead of quietly
    /// reporting totals that are short by a third (#252).
    ///
    /// Liveness is the file's own write time against the same window that
    /// decides what to attach to, so attaching and counting answer to one rule.
    /// Counting tailers instead made this a high-water mark that never fell:
    /// nothing detaches a file for going quiet - only five consecutive read
    /// FAILURES do - and a file that has stopped growing still polls
    /// successfully with zero lines. The warning therefore could not fire for
    /// the live character switch it exists to catch, only for a box that was
    /// already silent when the tool started.
    ///
    /// A character in the world emits periodic autohit and status lines, the
    /// same property <see cref="Sessions.SessionTracker.IdleTimeout"/> rests
    /// on, so a file silent for the whole window is a character who has gone or
    /// stopped logging rather than one standing still.
    /// </summary>
    public int AttachedAccounts
    {
        get
        {
            DateTime cutoff = DateTime.UtcNow - _attachWindow;
            HashSet<string> live = new(StringComparer.OrdinalIgnoreCase);
            foreach (string file in _tailers.Keys)
            {
                // A file that has been deleted reads as 1601 rather than
                // throwing, which is the answer wanted anyway: gone is not live.
                if (File.GetLastWriteTimeUtc(file) >= cutoff)
                {
                    live.Add(ChatLogTree.AccountFor(file));
                }
            }

            return live.Count;
        }
    }

    public IReadOnlyList<WatchBatch> Poll()
    {
        if (_pollsSinceDiscovery == 0)
        {
            Discover();
        }

        _pollsSinceDiscovery = (_pollsSinceDiscovery + 1) % _discoveryInterval;

        List<WatchBatch> batches = [];
        List<string> detached = [];

        // Files are keyed by full path, so one account's daily logs are
        // contiguous and in chronological order. If an older one stops at its
        // line cap, its newer siblings wait: reading them first would hand the
        // tracker an account's lines out of order, and the whole
        // replay-equals-live guarantee rests on that never happening.
        string? backlogged = null;
        foreach ((string file, ChatLogTailer tailer) in _tailers)
        {
            string account = ChatLogTree.AccountFor(file);
            if (string.Equals(account, backlogged, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryPoll(tailer, out IReadOnlyList<string> lines))
            {
                if (Fail(file))
                {
                    detached.Add(file);
                }

                continue;
            }

            _failures.Remove(file);
            if (tailer.HasMore)
            {
                backlogged = account;
            }

            if (lines.Count > 0)
            {
                batches.Add(new WatchBatch(account, lines));
            }
        }

        foreach (string file in detached)
        {
            _tailers[file].Dispose();
            _tailers.Remove(file);
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

    /// <summary>
    /// Transient read failures (disk, AV, a share change) are the caller's to
    /// count: a file that keeps failing is dropped so discovery can re-attach
    /// it, and reported so it never vanishes silently from the summary.
    /// </summary>
    private static bool TryPoll(ChatLogTailer tailer, out IReadOnlyList<string> lines)
    {
        try
        {
            lines = tailer.Poll();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            lines = [];
            return false;
        }
    }

    private bool Fail(string file)
    {
        int count = _failures.GetValueOrDefault(file) + 1;
        _failures[file] = count;
        if (count < FailuresBeforeDetach)
        {
            return false;
        }

        _failures.Remove(file);
        _unreadable.Add(file);
        return true;
    }

    private void Discover()
    {
        DateTime cutoff = DateTime.UtcNow - _attachWindow;
        foreach (string file in ChatLogTree.EnumerateLogs(_root))
        {
            if (_tailers.Count >= MaxTailers)
            {
                return;
            }

            if (_tailers.ContainsKey(file) || File.GetLastWriteTimeUtc(file) < cutoff)
            {
                continue;
            }

            try
            {
                _tailers.Add(file, new ChatLogTailer(file));
                _unreadable.Remove(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _unreadable.Add(file); // locked or denied: reported, retried next discovery
            }
        }
    }
}
