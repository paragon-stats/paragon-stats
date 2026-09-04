namespace ParagonStats.Core.Parsing;

/// <summary>
/// An autohit line that names someone, where the power is not self-only so the
/// name cannot be trusted on its own.
///
/// <see cref="IdentityPulse"/> is proof; this is a lead. Health and Stamina are
/// universal inherents that only ever affect their owner, which is what makes
/// the pulse trustworthy. Every other power is build-specific, and broadening
/// the pulse grammar to accept them is a data-corruption bug: measured over one
/// account's history, "HIT (name)! Your (power) power is autohit." named 778
/// distinct people for a single Incarnate power, because it buffs everyone in
/// the vicinity - 775 of them bystanders. Judgement powers name the enemies
/// they land on.
///
/// So the parser reports the lead and refuses to rule on it. <see cref="Sessions.SessionTracker"/>
/// promotes a candidate to an identity only when the name is one it has already
/// seen in a login banner on that same account - which no enemy, pet or other
/// player can be. That test needs no knowledge of powers at all, so it does not
/// go stale when a player rolls a build nobody anticipated.
/// </summary>
public sealed record AutohitCandidate(string CharacterName) : LogEvent(EventCategory.Identity);
