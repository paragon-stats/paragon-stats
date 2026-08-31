namespace ParagonStats.Core.Parsing;

/// <summary>The login banner: "Welcome to City of Heroes, Name!" - the only per-character session anchor.</summary>
public sealed record SessionStart(string CharacterName)
    : LogEvent(EventCategory.Session);
