namespace ParagonStats.Core.Parsing;

/// <summary>
/// Architect Entertainment tickets from a defeat or mission bonus - the farm
/// economy's primary reward: an AE farm pays tickets INSTEAD of influence, so
/// a farming dashboard that only tracks influence reports its best sessions
/// as zero.
/// </summary>
public sealed record TicketsEarned(long Count) : LogEvent(EventCategory.Reward);
