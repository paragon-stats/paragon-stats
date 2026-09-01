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
    private int _opened;

    public long UnattributedCount { get; private set; }

    public IReadOnlyCollection<CharacterSession> Open => _current.Values;

    /// <summary>
    /// Chronological, and TOTALLY ordered. List.Sort is unstable, so a key
    /// that leaves ties lets two replays of the same lines emit two different
    /// lists - and same-second sessions are routine (a pulse-opened sliver
    /// and the banner behind it, a relog). Start, then account, then
    /// character, then open order separates every pair. The first three are
    /// properties of the content, so the order never depends on which file or
    /// which tick delivered a session; open order is only ever consulted
    /// within one account, where it is the same in batch and live because the
    /// watcher holds an account's newer files back until its older ones have
    /// caught up (see LogWatcher.Poll), so an account's lines arrive in the
    /// same order either way.
    /// Account strings are compared case-sensitively here while the open-session
    /// map keys them case-insensitively; that only matters if a caller supplies
    /// one account directory under two casings, and the result stays
    /// deterministic either way because the comparison is on content.
    /// </summary>
    public IReadOnlyList<CharacterSession> Sessions
    {
        get
        {
            List<CharacterSession> all = [.. _closed, .. _current.Values];
            all.Sort(Compare);
            return all;
        }
    }

    /// <summary>
    /// The live-watch stop authority: the game client exited, so every open
    /// session closes at its last-line timestamp.
    /// </summary>
    public void CloseAll()
    {
        foreach (CharacterSession session in _current.Values)
        {
            _closed.Add(session);
        }

        _current.Clear();
    }

    /// <summary>
    /// Raw-line entry: reader then parser then fold, one idiom for batch and
    /// live. False when the line was skipped or refused (never collected).
    /// </summary>
    public bool Accept(string account, string rawLine)
    {
        if (LogLineReader.TryParse(rawLine, out LogLine line) && LineParser.TryParse(line, out LogEvent logEvent))
        {
            Accept(account, line, logEvent);
            return true;
        }

        return false;
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

            session = new CharacterSession(account, identified, line.Timestamp, _opened++);
            _current[account] = session;
        }
        else if (session is null)
        {
            UnattributedCount++;
            return;
        }

        // The trigger line belongs to the session it opens: counted and
        // captured like every other line. Communication channels never reach
        // this point - the parser dumps them (zero collection, by ruling).
        session.LastSeen = line.Timestamp;
        session.Stats.Apply(logEvent);
        session.Messages.Add(line.Timestamp, logEvent.Category, line.Payload);
    }

    /// <summary>Ordinal, never culture: culture-sensitive comparison would make the order machine-dependent.</summary>
    private static int Compare(CharacterSession left, CharacterSession right)
    {
        int order = left.Start.CompareTo(right.Start);
        if (order != 0)
        {
            return order;
        }

        order = StringComparer.Ordinal.Compare(left.Account, right.Account);
        if (order != 0)
        {
            return order;
        }

        order = StringComparer.Ordinal.Compare(left.Character, right.Character);
        return order != 0 ? order : left.Sequence.CompareTo(right.Sequence);
    }
}
