namespace ParagonStats.Core.Parsing;

/// <summary>"You have defeated Foe" (Attacker null) or "Name has defeated Foe".</summary>
public sealed record Defeat(string? Attacker, string Foe)
    : LogEvent(EventCategory.Defeat);
