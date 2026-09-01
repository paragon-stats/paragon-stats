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
/// The verdict is INCREMENTAL and FAIL-CLOSED: a partial line is refused the
/// moment it can be, held only while genuinely undecidable, and refused
/// outright if it is still undecidable at <see cref="MaxClassifyLength"/>.
/// This is the one home for the rule - the reader and the parser both ask
/// here, so the two gates can never drift apart.
/// Residual exposure is stated honestly: the plaintext chatlog files on disk,
/// the OS file cache, GC copies of collected lines, and the game's own
/// process memory are outside this tool's control.
/// </summary>
public static class CollectionPolicy
{
    /// <summary>Length of the "yyyy-MM-dd HH:mm:ss " stamp every data line carries.</summary>
    public const int TimestampLength = 20;

    /// <summary>
    /// A line still undecidable here is refused: no real data line opens with
    /// this much leading whitespace, and the longest refused literal is 25
    /// chars, so anything longer is either padding or an attack on the window.
    /// </summary>
    public const int MaxClassifyLength = 256;

    private static readonly string[] RefusedPrefixes =
    [
        "[",
        "Using global chat handle ",
        "Joined channel ",
        "Left channel ",
    ];

    /// <summary>Verdict for a whole line, timestamp included. Undecidable counts as refused.</summary>
    public static bool Refuses(ReadOnlySpan<char> line) => Classify(line, complete: true) == CollectionVerdict.Refuse;

    /// <summary>Verdict for a payload with the timestamp already stripped (the parser's gate).</summary>
    public static bool RefusesPayload(ReadOnlySpan<char> payload) =>
        ClassifyPayload(payload, complete: true) == CollectionVerdict.Refuse;

    /// <summary>
    /// Verdict for a line that may still be arriving. <paramref name="complete"/>
    /// is true when no more characters can change the answer (end of line, or
    /// the classification cap) - undecidable then means refused.
    /// </summary>
    public static CollectionVerdict Classify(ReadOnlySpan<char> line, bool complete)
    {
        if (!TimestampShapeHolds(line))
        {
            return CollectionVerdict.Refuse;
        }

        if (line.Length < TimestampLength)
        {
            return complete ? CollectionVerdict.Refuse : CollectionVerdict.Undecided;
        }

        return ClassifyPayload(line[TimestampLength..], complete || line.Length >= MaxClassifyLength);
    }

    /// <summary>
    /// The "yyyy-MM-dd HH:mm:ss " shape, checked as far as the line has
    /// arrived: a mismatch at any fixed position means a continuation line -
    /// communication content without a timestamp - refused immediately.
    /// </summary>
    private static bool TimestampShapeHolds(ReadOnlySpan<char> line)
    {
        for (int i = 0; i < TimestampLength && i < line.Length; i++)
        {
            char expected = i switch
            {
                4 or 7 => '-',
                10 or 19 => ' ',
                13 or 16 => ':',
                _ => '\0',
            };

            bool holds = expected == '\0' ? char.IsAsciiDigit(line[i]) : line[i] == expected;
            if (!holds)
            {
                return false;
            }
        }

        return true;
    }

    private static CollectionVerdict ClassifyPayload(ReadOnlySpan<char> payload, bool complete)
    {
        // TrimStart() with no argument covers every whitespace character, not
        // just U+0020: a tab or NBSP must not walk a chat line past the gate.
        ReadOnlySpan<char> trimmed = payload.TrimStart();
        if (trimmed.IsEmpty)
        {
            return complete ? CollectionVerdict.Refuse : CollectionVerdict.Undecided;
        }

        // Every line the game states as data opens with a letter or a digit -
        // "You gain", "HIT", a character name, "Entering". Anything else
        // opening a line is markup or a channel tag, including bracket
        // characters ASCII TrimStart/StartsWith would never recognise
        // (fullwidth, angle, or any other confusable). Fail closed: an
        // unrecognised opener is refused, not collected.
        if (!char.IsLetterOrDigit(trimmed[0]))
        {
            return CollectionVerdict.Refuse;
        }

        bool couldStillMatch = false;
        foreach (string prefix in RefusedPrefixes)
        {
            if (trimmed.Length >= prefix.Length)
            {
                if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return CollectionVerdict.Refuse;
                }
            }
            else if (prefix.AsSpan(0, trimmed.Length).SequenceEqual(trimmed))
            {
                couldStillMatch = true;
            }
        }

        if (!couldStillMatch)
        {
            return CollectionVerdict.Collect;
        }

        return complete ? CollectionVerdict.Refuse : CollectionVerdict.Undecided;
    }
}
