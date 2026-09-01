namespace ParagonStats.Core.Parsing;

/// <summary>"You gain N experience[ and M influence|infamy]." - either part may be absent.</summary>
public sealed record RewardGained(long? Experience, long? Influence)
    : LogEvent(EventCategory.Reward);
