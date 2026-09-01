using System.Text.Json;

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

        foreach (string candidate in (string[])[Path.Join(gameRoot, "accounts"), gameRoot])
        {
            if (Directory.Exists(candidate)
                && Directory.EnumerateFiles(candidate, "chatlog*.txt", SearchOption.AllDirectories)
                    .Any(f => string.Equals(new FileInfo(f).Directory?.Name, "Logs", StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return null;
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

    public void SaveGameRoot(string gameRoot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(new AppConfig { GameRoot = gameRoot }, AppConfigContext.Default.AppConfig));
    }
}
