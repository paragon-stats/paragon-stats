namespace ParagonStats.Core.Parsing;

/// <summary>
/// A categorized chat-log event. The taxonomy is sized to the MVP metrics;
/// every line the parser does not recognize becomes <see cref="UncategorizedLine"/> -
/// never dropped, never thrown on.
/// </summary>
public abstract record LogEvent(EventCategory Category);
