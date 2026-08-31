namespace ParagonStats.Core.Logging;

/// <summary>A single timestamped chat-log line: when it happened and the text after the timestamp.</summary>
public readonly record struct LogLine(DateTime Timestamp, string Payload);
