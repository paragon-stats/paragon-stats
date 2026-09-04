using ParagonStats.Core.Sessions;

namespace ParagonStats.Core.Stats;

/// <summary>
/// Result of replaying files: sessions, what could not be attributed - as a
/// count AND as value, because a count alone hid 1,864,215 XP behind the word
/// "unattributed" (#251) - and files skipped as unreadable.
/// </summary>
public sealed record ReplayResult(
    IReadOnlyList<CharacterSession> Sessions,
    long UnattributedCount,
    IReadOnlyList<string> SkippedFiles,
    long UnattributedExperience = 0,
    long UnattributedInfluence = 0);
