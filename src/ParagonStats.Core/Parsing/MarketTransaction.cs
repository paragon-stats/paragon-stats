namespace ParagonStats.Core.Parsing;

/// <summary>
/// Consignment House / Black Market money movement. Tracked apart from combat
/// rewards: market income realizes when goods sell, not when the underlying
/// play happened, so folding it into combat influence corrupts rate math.
/// </summary>
public sealed record MarketTransaction(long Amount, bool Income) : LogEvent(EventCategory.Market);
