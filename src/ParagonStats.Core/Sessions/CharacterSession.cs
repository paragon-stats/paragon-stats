using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Sessions;

/// <summary>
/// One character's play window on one account: opened by a login banner,
/// closed by the next banner on the same account (or end of input).
/// </summary>
public sealed class CharacterSession
{
    internal CharacterSession(string account, string character, DateTime start)
    {
        this.Account = account;
        this.Character = character;
        this.Start = start;
        this.LastSeen = start;
    }

    public string Account { get; }

    public string Character { get; }

    public DateTime Start { get; }

    public DateTime LastSeen { get; internal set; }

    public SessionStats Stats { get; } = new();

    public MessageLog Messages { get; } = new();
}
