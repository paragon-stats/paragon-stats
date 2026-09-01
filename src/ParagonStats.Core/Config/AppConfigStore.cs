using System.Text.Json;

using ParagonStats.Core.Logging;

namespace ParagonStats.Core.Config;

/// <summary>
/// The saved game-client location (operator directive: manual on first
/// launch, re-validated every launch, re-prompted when the directory check
/// fails). A missing or malformed file reads as first launch, never throws.
/// </summary>
public sealed class AppConfigStore
{
    private readonly string _path;

    public AppConfigStore(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _path = path;
    }

    /// <summary>
    /// The directory check: a root is valid when it (or its accounts child)
    /// holds at least one chatlog under a Logs directory - the shape the
    /// game actually writes.
    /// </summary>
    public static string? ResolveAccountsDir(string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return null;
        }

        string accountsChild = Path.Join(gameRoot, "accounts");
        if (Qualifies(accountsChild))
        {
            return accountsChild;
        }

        return Qualifies(gameRoot) ? gameRoot : null;

        static bool Qualifies(string candidate) =>
            Directory.Exists(candidate)
            && Directory.EnumerateFiles(candidate, ChatLogTree.FilePattern, ChatLogTree.SafeRecurse).Any(ChatLogTree.IsUnderLogs);
    }

    public string? LoadGameRoot()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(_path), AppConfigContext.Default.AppConfig)?.GameRoot;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>False when the config location is unwritable - the caller warns; the run continues.</summary>
    public bool TrySaveGameRoot(string gameRoot)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new AppConfig { GameRoot = gameRoot }, AppConfigContext.Default.AppConfig));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
