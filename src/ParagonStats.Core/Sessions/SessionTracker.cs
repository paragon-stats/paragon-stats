using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Sessions;

/// <summary>
/// Per-account session state machine. Two identity triggers open sessions: the
/// <see cref="SessionStart"/> banner, and the <see cref="IdentityPulse"/>
/// self-autohit heartbeat (which covers the banner lagging a character swap by
/// up to ~30s, or missing the log entirely at first login - observed live and
/// verified against all 435 historical swaps, zero missed). A banner always
/// opens a new session; a pulse opens one only when it names a different
/// character than the open session (or none is open). Lines outside a
/// trigger-anchored session are counted but unattributed: the login flow
/// (account, server, character select, login) means the active character can
/// be anyone on the account, so attribution waits for proof.
/// The logs contain no logout line (every quit/leave line in the full source
/// is an in-game team/league/SG/TF/AE mechanic), so a silence of
/// <see cref="IdleTimeout"/> closes the session: an in-game character emits
/// periodic autohit/status lines, and the game's own AFK-logout safety ends a
/// silent character at that same mark. Sessions are keyed by account, not by
/// file, so daily log rollover is transparent.
/// </summary>
public sealed class SessionTracker
{
    /// <summary>The game client's AFK-logout safety duration (operator-confirmed):
    /// silence past this means the character is logged out.</summary>
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

        if (_current.TryGetValue(account, out CharacterSession? session)
            && line.Timestamp - session.LastSeen >= IdleTimeout)
        {
            _closed.Add(session);
            _current.Remove(account);
            session = null;
        }

        // A banner always opens a session (a relog of the same character is a
        // new session); a pulse opens one only on mismatch or when none is open.
        string? identified = logEvent switch
        {
            SessionStart start => start.CharacterName,
            IdentityPulse pulse when session is null || !string.Equals(pulse.CharacterName, session.Character, StringComparison.Ordinal) => pulse.CharacterName,
            _ => null,
        };

        if (identified is not null)
        {
            if (session is not null)
            {
                _closed.Add(session);
            }

            session = new CharacterSession(account, identified, line.Timestamp);
            _current[account] = session;
        }
        else if (session is null)
        {
            UnattributedCount++;
            return;
        }

        // The trigger line belongs to the session it opens: counted and
        // captured like every other line, so nothing is ever dropped.
        session.LastSeen = line.Timestamp;
        session.Stats.Apply(logEvent);
        string? channel = logEvent is ChatMessage chat ? chat.Channel : null;
        session.Messages.Add(line.Timestamp, logEvent.Category, channel, line.Payload);
    }
}
