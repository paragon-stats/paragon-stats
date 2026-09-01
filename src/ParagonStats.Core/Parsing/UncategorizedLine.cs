namespace ParagonStats.Core.Parsing;

/// <summary>Any line the parser does not recognize; the payload passes through untouched.</summary>
public sealed record UncategorizedLine(string Payload)
    : LogEvent(EventCategory.Uncategorized);
