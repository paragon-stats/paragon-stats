using ParagonStats.Core.Sessions;

namespace ParagonStats.Core.Stats;

/// <summary>Result of replaying files: sessions, unattributable-line count, and files skipped as unreadable.</summary>
public sealed record ReplayResult(IReadOnlyList<CharacterSession> Sessions, long UnattributedCount, IReadOnlyList<string> SkippedFiles);
