using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Sessions;

/// <summary>
/// Per-account session state machine. A <see cref="SessionStart"/> banner closes
/// the account's current session and opens a new one; every other event is
/// attributed to the current session. Lines seen before the first banner are
/// counted but unattributable (chat logging can begin mid-session). Sessions are
/// keyed by account, not by file, so daily log rollover is transparent.
/// </summary>
public sealed class SessionTracker
{
    private readonly Dictionary<string, CharacterSession> _current = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CharacterSession> _closed = [];

    public long UnattributedCount { get; private set; }

    public IReadOnlyList<CharacterSession> Sessions
    {
        get
        {
            List<CharacterSession> all = [.. _closed, .. _current.Values];
            all.Sort((a, b) => a.Start.CompareTo(b.Start));
            return all;
        }
    }

    public void Accept(string account, in LogLine line, LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(logEvent);

        if (logEvent is SessionStart start)
        {
            if (_current.Remove(account, out CharacterSession? finished))
            {
                _closed.Add(finished);
            }

            _current[account] = new CharacterSession(account, start.CharacterName, line.Timestamp);
            return;
        }

        if (!_current.TryGetValue(account, out CharacterSession? session))
        {
            UnattributedCount++;
            return;
        }

        session.LastSeen = line.Timestamp;
        session.Stats.Apply(logEvent);
        session.Messages.Add(line.Timestamp, logEvent.Category, line.Payload);
    }
}
