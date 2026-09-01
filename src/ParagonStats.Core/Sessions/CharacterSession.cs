using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Sessions;

/// <summary>
/// One character's play window on one account: opened by a login banner,
/// closed by the next banner on the same account, an idle gap, or end of
/// input.
/// </summary>
public sealed class CharacterSession
{
    internal CharacterSession(string account, string character, DateTime start, int sequence)
    {
        Account = account;
        Character = character;
        Start = start;
        LastSeen = start;
        Sequence = sequence;
    }

    public string Account { get; }

    public string Character { get; }

    public DateTime Start { get; }

    public DateTime LastSeen { get; internal set; }

    public SessionStats Stats { get; } = new();

    public MessageLog Messages { get; } = new();

    /// <summary>
    /// Open order within the tracker - the last tiebreak of the session sort,
    /// for the pairs the content keys cannot separate: a relog of the same
    /// character on the same account inside one second, and a pulse-opened
    /// sliver followed by the banner that lands in the same second.
    /// </summary>
    internal int Sequence { get; }
}
