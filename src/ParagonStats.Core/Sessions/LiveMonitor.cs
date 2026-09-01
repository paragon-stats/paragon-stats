using ParagonStats.Core.Logging;

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
    private bool _clientWasRunning = true;

    public LiveMonitor(LogWatcher watcher, SessionTracker tracker, Func<bool> clientRunning)
    {
        ArgumentNullException.ThrowIfNull(watcher);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(clientRunning);
        _watcher = watcher;
        _tracker = tracker;
        _clientRunning = clientRunning;
    }

    /// <summary>Returns the number of COLLECTED lines this tick (dumped communication lines count as nothing, even here).</summary>
    public long Tick()
    {
        // Read the client state BEFORE polling: lines the client flushed on
        // its way out are drained into their sessions, then closed.
        bool running = _clientRunning();

        long count = 0;
        foreach (WatchBatch batch in _watcher.Poll())
        {
            foreach (string raw in batch.Lines)
            {
                if (_tracker.Accept(batch.Account, raw))
                {
                    count++;
                }
            }
        }

        // Edge-triggered: close once on the running->gone transition. A
        // level-triggered close would fragment sessions every tick if the
        // process check ever misreads (renamed client binary).
        if (_clientWasRunning && !running)
        {
            _tracker.CloseAll();
        }

        _clientWasRunning = running;
        return count;
    }
}
