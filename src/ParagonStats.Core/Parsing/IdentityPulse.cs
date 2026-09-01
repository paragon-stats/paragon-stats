namespace ParagonStats.Core.Parsing;

/// <summary>
/// A line proving which character is active: the Health/Stamina autohit pair.
/// Those inherent powers can only target their owner, so the named character
/// is the logged-in one - teammates' auras can never appear in this shape.
/// Fires within seconds of login (before the banner, which can lag the
/// character swap by up to ~30s or miss the log entirely at first login) and
/// every ~15s thereafter.
/// </summary>
public sealed record IdentityPulse(string CharacterName) : LogEvent(EventCategory.Identity);
