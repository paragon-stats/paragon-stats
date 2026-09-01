using ParagonStats.Core.Config;
using ParagonStats.Core.Logging;
using ParagonStats.Core.Sessions;

namespace ParagonStats.Core.Stats;

/// <summary>
/// The CLI's whole behavior, in Core so it is testable and coverage-measured:
/// resolve the game location (explicit path wins and refreshes the saved
/// value; otherwise the saved config, prompting on first launch and whenever
/// the directory check fails), then batch-replay or live-watch.
/// Program.cs stays a thin shim.
/// </summary>
public static class CliRunner
{
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error) =>
        Run(args, output, error, new CliEnvironment());

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, CliEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(env);

        bool watch = args.Contains("--watch", StringComparer.Ordinal);
        List<string> positional = [.. args.Where(a => !string.Equals(a, "--watch", StringComparison.Ordinal))];
        if (positional.Count > 1)
        {
            Fail(error, "usage: paragon-stats [--watch] [chatlog-file-or-game-directory]");
            return 2;
        }

        string? target = positional.Count == 1 ? positional[0] : null;
        if (target is not null && File.Exists(target))
        {
            if (watch)
            {
                Fail(error, "--watch needs a directory, not a file");
                return 1;
            }

            return Replay([target], target, output, error);
        }

        string? accounts = ResolveRoot(target, output, error, env);
        if (accounts is null)
        {
            return 1;
        }

        return watch ? Watch(accounts, output, env) : ReplayDirectory(accounts, output, error);
    }

    /// <summary>
    /// Explicit directory: used as-is (and saved when it passes the directory
    /// check). No path: the saved root, prompting on first launch and
    /// whenever the check fails (moved install, unplugged drive).
    /// </summary>
    private static string? ResolveRoot(string? target, TextWriter output, TextWriter error, CliEnvironment env)
    {
        AppConfigStore store = new(env.ConfigPath);
        if (target is not null)
        {
            if (!Directory.Exists(target))
            {
                Fail(error, $"no chatlog files found at: {target}");
                return null;
            }

            string? resolved = AppConfigStore.ResolveAccountsDir(target);
            if (resolved is not null)
            {
                Save(store, target, output);
            }

            return resolved ?? target;
        }

        string? saved = store.LoadGameRoot();
        string? accounts = saved is null ? null : AppConfigStore.ResolveAccountsDir(saved);
        string? root = saved;
        bool fromConfig = saved is not null;
        while (accounts is null)
        {
            output.WriteLine((root, fromConfig) switch
            {
                (null, _) => "First launch: enter the Homecoming install location (e.g. C:\\Games\\Homecoming):",
                (_, true) => $"Saved game location has no chatlogs ({root}); enter the Homecoming install location:",
                _ => $"No chatlogs found at {root}; enter the Homecoming install location:",
            });
            root = env.Input?.ReadLine();
            fromConfig = false;
            if (string.IsNullOrWhiteSpace(root))
            {
                Fail(error, "no game location provided");
                return null;
            }

            accounts = AppConfigStore.ResolveAccountsDir(root);
        }

        if (!string.Equals(root, saved, StringComparison.Ordinal))
        {
            Save(store, root!, output);
        }

        return accounts;
    }

    private static int ReplayDirectory(string directory, TextWriter output, TextWriter error)
    {
        string[] files = [.. Directory.EnumerateFiles(directory, ChatLogTree.FilePattern, ChatLogTree.SafeRecurse)];

        // Daily chatlog names sort ordinally into chronological order per account.
        Array.Sort(files, StringComparer.Ordinal);
        return Replay(files, directory, output, error);
    }

    private static int Replay(string[] files, string target, TextWriter output, TextWriter error)
    {
        if (files.Length == 0)
        {
            Fail(error, $"no chatlog files found at: {target}");
            return 1;
        }

        output.Write(SummaryFormatter.Format(LogReplayer.Replay(files)));
        return 0;
    }

    private static int Watch(string accountsDir, TextWriter output, CliEnvironment env)
    {
        output.WriteLine($"watching {accountsDir} - Ctrl+C for the session summary");
        SessionTracker tracker = new();
        using LogWatcher watcher = new(accountsDir, SessionTracker.IdleTimeout);
        LiveMonitor monitor = new(watcher, tracker, env.ClientRunning);
        while (!env.Token.IsCancellationRequested)
        {
            if (monitor.Tick() > 0)
            {
                foreach (CharacterSession session in tracker.Open)
                {
                    output.WriteLine(SummaryFormatter.FormatLive(session));
                }

                if (tracker.Open.Count > 1)
                {
                    output.WriteLine(SummaryFormatter.FormatCombined(tracker.Open));
                }
            }

            env.Sleep(500);
        }

        output.Write(SummaryFormatter.Format(new ReplayResult(tracker.Sessions, tracker.UnattributedCount, [.. watcher.Unreadable])));
        return 0;
    }

    private static void Save(AppConfigStore store, string gameRoot, TextWriter output)
    {
        if (!store.TrySaveGameRoot(gameRoot))
        {
            output.WriteLine("warning: could not save the game location; you will be prompted again next launch");
        }
    }

    private static void Fail(TextWriter error, string message) => error.WriteLine(message);
}
