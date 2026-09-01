using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Sessions;

/// <summary>
/// One live-watch step: pull new lines from every tailed log into the session
/// tracker, then apply the stop authority - when the game client process is
/// gone, every open session closes at its last-line timestamp (operator
/// ruling; the in-log triggers and the AFK-silence rule handle everything
/// else unchanged).
/// </summary>
public sealed class LiveMonitor
{
    private readonly LogWatcher _watcher;
    private readonly SessionTracker _tracker;
    private readonly Func<bool> _clientRunning;

    public LiveMonitor(LogWatcher watcher, SessionTracker tracker, Func<bool> clientRunning)
    {
        ArgumentNullException.ThrowIfNull(watcher);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(clientRunning);
        _watcher = watcher;
        _tracker = tracker;
        _clientRunning = clientRunning;
    }

    /// <summary>Returns the number of new raw lines seen this tick.</summary>
    public long Tick()
    {
        long count = 0;
        foreach (WatchBatch batch in _watcher.Poll())
        {
            foreach (string raw in batch.Lines)
            {
                count++;
                if (LogLineReader.TryParse(raw, out LogLine line))
                {
                    _tracker.Accept(batch.Account, line, LineParser.Parse(line));
                }
            }
        }

        if (!_clientRunning())
        {
            _tracker.CloseAll();
        }

        return count;
    }
}
