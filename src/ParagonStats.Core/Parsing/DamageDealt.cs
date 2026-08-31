namespace ParagonStats.Core.Parsing;

/// <summary>
/// "You hit Target with your Power for N points of Type damage[ over time]." -
/// optionally prefixed by a pseudopet source ("Irradiated Ground:  ").
/// </summary>
public sealed record DamageDealt(
    string Target,
    string Power,
    decimal Amount,
    string DamageType,
    bool OverTime,
    string? SourcePrefix)
    : LogEvent(EventCategory.Damage);
