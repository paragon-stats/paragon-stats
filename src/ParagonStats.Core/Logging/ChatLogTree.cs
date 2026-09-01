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
    /// Bounded enumeration: survives access-denied subdirectories instead of
    /// throwing mid-iteration, refuses to follow reparse points (a junction
    /// or symlink cycle would otherwise be walked until the path length
    /// explodes, opening a handle per synthetic path), and stops well below
    /// the real layout's depth (accounts/name/Logs = 3).
    /// </summary>
    public static readonly EnumerationOptions SafeRecurse = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        MaxRecursionDepth = 8,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
    };

    /// <summary>
    /// Every chatlog under a root that this tool will read: the game's shape
    /// only (least collection - a chatlog elsewhere under the root is not
    /// ours), bounded by <see cref="SafeRecurse"/>, and empty rather than
    /// fatal when the tree cannot be walked (a drive pulled mid-scan, a root
    /// that is not a directory). The one discovery path: watcher, batch
    /// replay, and the game-location check all come through here.
    /// </summary>
    public static IReadOnlyList<string> EnumerateLogs(string root)
    {
        try
        {
            return [.. Directory.EnumerateFiles(root, FilePattern, SafeRecurse).Where(IsUnderLogs)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

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
