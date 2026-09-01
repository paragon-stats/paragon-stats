namespace ParagonStats.Core.Parsing;

/// <summary>Any line the parser does not recognize; the payload passes through untouched.</summary>
public sealed record UncategorizedLine(string Payload)
    : LogEvent(EventCategory.Uncategorized)
{
    /// <summary>Allocation-free out-value for refused lines (never surfaced).</summary>
    public static readonly UncategorizedLine Empty = new(string.Empty);
}
