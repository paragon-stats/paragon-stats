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
/// <param name="CharacterName">Whoever the line named.</param>
/// <param name="SelfDirected">
/// True when the line is one of YOUR powers reaching the named character, which
/// a vicinity power does to you as well - so the name can be you. False when
/// the line names the CASTER of a power that landed on you, who for a power
/// that is not self-only is by definition somebody else, since your own casts
/// are logged in the other direction. Only a self-directed candidate can ever
/// identify the seated character, so the tracker refuses the rest outright
/// rather than leaning on the roster to catch them.
/// </param>
public sealed record AutohitCandidate(string CharacterName, bool SelfDirected) : LogEvent(EventCategory.Identity);
