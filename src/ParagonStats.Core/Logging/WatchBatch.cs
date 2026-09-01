namespace ParagonStats.Core.Logging;

/// <summary>New complete lines from one account's live log stream.</summary>
public sealed record WatchBatch(string Account, IReadOnlyList<string> Lines);
