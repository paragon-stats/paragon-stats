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

    /// <summary>
    /// Events read before anyone is identified are held, not dropped. A farm
    /// puts a few hundred lines a minute into this, and the window closes as
    /// soon as an autohit names the character, so the cap is generous rather
    /// than tight - it exists to bound a log that never identifies at all.
    /// </summary>
    private const int MaxHeldPerAccount = 20_000;

    private readonly Dictionary<string, CharacterSession> _current = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Held> _held = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _roster = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CharacterSession> _closed = [];
    private int _opened;

    public long UnattributedCount { get; private set; }

    /// <summary>
    /// What the unattributed lines were WORTH. A count alone cannot distinguish
    /// nine lines of login chatter from a fifth of a farming session; both read
    /// as "unattributed". Measured live, 1,864,215 XP went missing behind a
    /// count (#251).
    /// </summary>
    public long UnattributedExperience { get; private set; }

    /// <inheritdoc cref="UnattributedExperience"/>
    public long UnattributedInfluence { get; private set; }

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

        if (logEvent is SessionStart banner)
        {
            // The roster is only ever fed by banners, which no enemy, pet or
            // other player can produce - that is what makes it a safe filter.
            Roster(account).Add(banner.CharacterName);
        }

        // A banner always opens a session (a relog of the same character is a
        // new session); a pulse opens one only on mismatch or when none is open.
        // A candidate is a pulse-shaped lead from a power that is not self-only,
        // believed only when the name is already on this account's roster (#250).
        string? identified = logEvent switch
        {
            SessionStart start => start.CharacterName,
            IdentityPulse pulse when Names(session, pulse.CharacterName) => pulse.CharacterName,
            AutohitCandidate candidate when Roster(account).Contains(candidate.CharacterName)
                && Names(session, candidate.CharacterName) => candidate.CharacterName,
            _ => null,
        };

        if (identified is not null)
        {
            if (session is not null)
            {
                _closed.Add(session);
            }

            session = OpenFor(account, identified, line, logEvent);
        }
        else if (session is null)
        {
            Tally(logEvent, +1);
            Hold(account, line, logEvent);
            return;
        }

        // The trigger line belongs to the session it opens: counted and
        // captured like every other line. Communication channels never reach
        // this point - the parser dumps them (zero collection, by ruling).
        session.LastSeen = line.Timestamp;
        session.Stats.Apply(logEvent);
        session.Messages.Add(line.Timestamp, logEvent.Category, line.Payload);
    }

    /// <summary>
    /// Ordinal, never culture: culture-sensitive comparison would make the
    /// order machine-dependent. Internal rather than private so the text UI
    /// orders its own copy the same way, instead of restating the total order
    /// somewhere the two could drift apart.
    /// </summary>
    internal static int Compare(CharacterSession left, CharacterSession right)
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

    private static bool Names(CharacterSession? session, string character) =>
        session is null || !string.Equals(character, session.Character, StringComparison.Ordinal);

    /// <summary>
    /// Opens a session for a newly identified character, adopting the events
    /// held for that account when - and only when - the trigger justifies it.
    /// A pulse says "this character is here, and has been", so the lines before
    /// it are theirs. A banner says "a login begins now", so what came before
    /// belongs to whoever was playing previously and is left unattributed
    /// rather than credited to the arrival (#251).
    /// </summary>
    private CharacterSession OpenFor(string account, string character, in LogLine line, LogEvent trigger)
    {
        _held.Remove(account, out Held? waiting);
        Queue<(LogLine Line, LogEvent Event)>? adopted =
            trigger is not SessionStart && waiting is { Events.Count: > 0 } ? waiting.Events : null;

        // The session genuinely spans the adopted events, so it starts where
        // they do. Anchoring it at the trigger would credit those earnings to a
        // window that did not contain them, and every rate read from that
        // window would run high.
        DateTime start = adopted is null ? line.Timestamp : adopted.Peek().Line.Timestamp;
        CharacterSession session = new(account, character, start, _opened++);
        _current[account] = session;

        if (adopted is null)
        {
            return session;
        }

        foreach ((LogLine held, LogEvent heldEvent) in adopted)
        {
            Tally(heldEvent, -1);
            session.LastSeen = held.Timestamp;
            session.Stats.Apply(heldEvent);
            session.Messages.Add(held.Timestamp, heldEvent.Category, held.Payload);
        }

        return session;
    }

    /// <summary>
    /// Holds an event nobody can yet be shown to have earned.
    /// Bounded three ways. Oldest-out at the cap, so a log that never identifies
    /// anyone cannot grow without limit. Flushed at a silence of
    /// <see cref="IdleTimeout"/>, because that silence IS a logout by the same
    /// rule this class already closes sessions on - nothing on its far side can
    /// belong to whoever is named next, and adopting across it would credit one
    /// character's play to another. And discarded outright by a banner, which
    /// announces a new login rather than an ongoing one (#251).
    /// </summary>
    private void Hold(string account, in LogLine line, LogEvent logEvent)
    {
        if (!_held.TryGetValue(account, out Held? waiting))
        {
            waiting = new Held();
            _held[account] = waiting;
        }

        if (waiting.Events.Count > 0 && line.Timestamp - waiting.Newest >= IdleTimeout)
        {
            waiting.Events.Clear();
        }

        if (waiting.Events.Count == MaxHeldPerAccount)
        {
            waiting.Events.Dequeue();
        }

        waiting.Events.Enqueue((line, logEvent));
        waiting.Newest = line.Timestamp;
    }

    /// <summary>Adds or removes an event from the unattributed books, value included.</summary>
    private void Tally(LogEvent logEvent, int sign)
    {
        UnattributedCount += sign;
        if (logEvent is RewardGained reward)
        {
            UnattributedExperience += sign * (reward.Experience ?? 0);
            UnattributedInfluence += sign * (reward.Influence ?? 0);
        }
    }

    private HashSet<string> Roster(string account)
    {
        if (!_roster.TryGetValue(account, out HashSet<string>? names))
        {
            names = new HashSet<string>(StringComparer.Ordinal);
            _roster[account] = names;
        }

        return names;
    }

    /// <summary>Events waiting on an identity, and when the newest of them arrived.</summary>
    private sealed class Held
    {
        public Queue<(LogLine Line, LogEvent Event)> Events { get; } = new();

        public DateTime Newest { get; set; }
    }
}
