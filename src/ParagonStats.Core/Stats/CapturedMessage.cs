using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Stats;

/// <summary>One captured line: when, what kind, and the raw payload.</summary>
public readonly record struct CapturedMessage(DateTime Timestamp, EventCategory Category, string Payload);
