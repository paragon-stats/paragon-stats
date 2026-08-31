namespace ParagonStats.Core.Parsing;

/// <summary>A player-chat line: "[Channel] Speaker: text" (color markup stripped).</summary>
public sealed record ChatMessage(string Channel, string Speaker, string Text)
    : LogEvent(EventCategory.Chat);
