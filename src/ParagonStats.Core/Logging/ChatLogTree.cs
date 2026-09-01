namespace ParagonStats.Core.Logging;

/// <summary>
/// The one home for the on-disk chatlog layout the game writes:
/// "accounts\name\Logs\chatlog YYYY-MM-DD.txt". Discovery, account keying,
/// and shape checks all derive from here so the convention cannot drift
/// across call sites.
/// </summary>
public static class ChatLogTree
{
    public const string FilePattern = "chatlog*.txt";

    /// <summary>
    /// Enumeration that survives access-denied subdirectories (junctions,
    /// permissions, cloud placeholders) instead of throwing mid-iteration.
    /// </summary>
    public static readonly EnumerationOptions SafeRecurse = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
    };

    /// <summary>
    /// The account is the directory above "Logs". Files outside that shape
    /// key on their own parent directory, so unrelated locations can never
    /// collapse into one shared account.
    /// </summary>
    public static string AccountFor(string file)
    {
        DirectoryInfo? parent = new FileInfo(file).Directory;
        return parent is not null && IsLogsDir(parent) && parent.Parent is not null
            ? parent.Parent.Name
            : parent?.FullName ?? "unknown";
    }

    /// <summary>True when the file sits under a "Logs" directory - the shape the game writes.</summary>
    public static bool IsUnderLogs(string file)
    {
        DirectoryInfo? parent = new FileInfo(file).Directory;
        return parent is not null && IsLogsDir(parent);
    }

    private static bool IsLogsDir(DirectoryInfo dir) =>
        string.Equals(dir.Name, "Logs", StringComparison.OrdinalIgnoreCase);
}
