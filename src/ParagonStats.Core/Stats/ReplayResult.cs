using ParagonStats.Core.Sessions;

namespace ParagonStats.Core.Stats;

/// <summary>Result of replaying files: the sessions plus unattributable-line count.</summary>
public sealed record ReplayResult(IReadOnlyList<CharacterSession> Sessions, long UnattributedCount);
