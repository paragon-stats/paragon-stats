namespace ParagonStats.Core.Parsing;

/// <summary>"You activated the Power power."</summary>
public sealed record PowerActivated(string Power)
    : LogEvent(EventCategory.PowerActivation);
