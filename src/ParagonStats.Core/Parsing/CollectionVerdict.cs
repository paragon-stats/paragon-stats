namespace ParagonStats.Core.Parsing;

/// <summary>What the collection policy has decided about a line so far.</summary>
public enum CollectionVerdict
{
    /// <summary>More characters are needed; nothing may be emitted yet.</summary>
    Undecided,

    /// <summary>Never collect: no event, no capture, no count.</summary>
    Refuse,

    /// <summary>A data line: safe to materialize and fold.</summary>
    Collect,
}
