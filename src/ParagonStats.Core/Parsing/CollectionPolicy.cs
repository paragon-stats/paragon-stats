namespace ParagonStats.Core.Parsing;

/// <summary>
/// The zero-collection rule (operator ruling), applied at the earliest
/// possible point - BEFORE a refused line is ever materialized as a managed
/// string - so a memory dump of this process does not carry player
/// communications the tool never needed. Refused: bracketed communication
/// lines (data-channel allowlist empty; [NPC]/[Caption] join only by
/// deliberate decision on #225), communication metadata (the player's global
/// handle, channel membership), and timestamp-less continuation lines (MOTD
/// blocks - communication content without a timestamp).
/// Residual exposure is stated honestly: the plaintext chatlog files on disk,
/// the OS file cache, and the game's own process memory are outside this
/// tool's control; within it, refused content is bounded to transient read
/// buffers scrubbed every poll.
/// </summary>
public static class CollectionPolicy
{
    /// <summary>Chars of a line needed to classify it: timestamp (20) + the longest refused prefix.</summary>
    public const int ClassifyLength = 45;

    /// <summary>
    /// True when the line must not be collected. Callable on a prefix of at
    /// least <see cref="ClassifyLength"/> chars (or the whole line if
    /// shorter); operates on spans so classification allocates nothing.
    /// </summary>
    public static bool Refuses(ReadOnlySpan<char> line)
    {
        if (line.Length < 21 || line[4] != '-' || line[10] != ' ' || line[16] != ':' || line[19] != ' ')
        {
            return true; // no timestamp: a continuation line, never data
        }

        ReadOnlySpan<char> payload = line[20..].TrimStart(' ');
        return payload.StartsWith("[", StringComparison.Ordinal)
            || payload.StartsWith("Using global chat handle ", StringComparison.Ordinal)
            || payload.StartsWith("Joined channel ", StringComparison.Ordinal)
            || payload.StartsWith("Left channel ", StringComparison.Ordinal);
    }
}
