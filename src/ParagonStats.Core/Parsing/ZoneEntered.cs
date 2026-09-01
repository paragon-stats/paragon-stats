namespace ParagonStats.Core.Parsing;

/// <summary>
/// A map/zone transition: "Entering <name>." fires on zone changes and on
/// entering mission instances - including AE farm maps, which makes it the
/// key for per-map efficiency rollups (a farm's tickets attribute to the most
/// recently entered map).
/// </summary>
public sealed record ZoneEntered(string Zone) : LogEvent(EventCategory.Zone);
