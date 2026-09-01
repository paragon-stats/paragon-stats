using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Sessions;

/// <summary>
/// Per-account session state machine. A <see cref="SessionStart"/> banner closes
/// the account's current session and opens a new one; every other event is
/// attributed to the current session. Lines seen before the first banner are
/// counted but unattributable (chat logging can begin mid-session). Sessions are
/// keyed by account, not by file, so daily log rollover is transparent.
/// The logs contain no logout line (verified against the full source), so a
/// silence of <see cref="IdleTimeout"/> also closes the session: an in-game
/// character emits periodic autohit/status lines, meaning a long-silent
/// account is logged out. A line arriving after such a gap without a banner
/// (the banner can miss the log when logging races login) opens a new session
/// for the account's last-known character.
/// </summary>
public sealed class SessionTracker
{
    /// <summary>Silence on an account that means the character logged out.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

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

            CharacterSession opened = new(account, start.CharacterName, line.Timestamp);
            _current[account] = opened;

            // The banner belongs to the session it opens: counted and captured
            // like every other line, so nothing is ever dropped.
            opened.Stats.Apply(logEvent);
            opened.Messages.Add(line.Timestamp, logEvent.Category, channel: null, line.Payload);
            return;
        }

        if (!_current.TryGetValue(account, out CharacterSession? session))
        {
            UnattributedCount++;
            return;
        }

        if (line.Timestamp - session.LastSeen >= IdleTimeout)
        {
            _closed.Add(session);
            session = new CharacterSession(account, session.Character, line.Timestamp);
            _current[account] = session;
        }

        session.LastSeen = line.Timestamp;
        session.Stats.Apply(logEvent);
        string? channel = logEvent is ChatMessage chat ? chat.Channel : null;
        session.Messages.Add(line.Timestamp, logEvent.Category, channel, line.Payload);
    }
}
