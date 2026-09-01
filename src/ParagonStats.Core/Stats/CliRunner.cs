using System.Globalization;

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
            error.WriteLine("usage: paragon-stats [--watch] [chatlog-file-or-game-directory]");
            return 2;
        }

        string? target = positional.Count == 1 ? positional[0] : null;
        if (target is not null && File.Exists(target))
        {
            return watch ? Fail(error, "--watch needs a directory, not a file") : Replay([target], target, output, error);
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
                _ = Fail(error, $"no chatlog files found at: {target}");
                return null;
            }

            string? resolved = AppConfigStore.ResolveAccountsDir(target);
            if (resolved is not null)
            {
                store.SaveGameRoot(target);
            }

            return resolved ?? target;
        }

        string? root = store.LoadGameRoot();
        string? saved = root;
        string? accounts = root is null ? null : AppConfigStore.ResolveAccountsDir(root);
        while (accounts is null)
        {
            output.WriteLine(root is null
                ? "First launch: enter the Homecoming install location (e.g. C:\\Games\\Homecoming):"
                : $"Saved game location has no chatlogs ({root}); enter the Homecoming install location:");
            root = env.Input?.ReadLine();
            if (string.IsNullOrWhiteSpace(root))
            {
                _ = Fail(error, "no game location provided");
                return null;
            }

            accounts = AppConfigStore.ResolveAccountsDir(root);
        }

        if (!string.Equals(root, saved, StringComparison.Ordinal))
        {
            store.SaveGameRoot(root!);
        }

        return accounts;
    }

    private static int ReplayDirectory(string directory, TextWriter output, TextWriter error)
    {
        string[] files = Directory.GetFiles(directory, "chatlog*.txt", SearchOption.AllDirectories);

        // Daily chatlog names sort ordinally into chronological order per account.
        Array.Sort(files, StringComparer.Ordinal);
        return Replay(files, directory, output, error);
    }

    private static int Replay(string[] files, string target, TextWriter output, TextWriter error)
    {
        if (files.Length == 0)
        {
            return Fail(error, $"no chatlog files found at: {target}");
        }

        output.Write(SummaryFormatter.Format(LogReplayer.Replay(files)));
        return 0;
    }

    private static int Watch(string accountsDir, TextWriter output, CliEnvironment env)
    {
        SessionTracker tracker = new();
        using LogWatcher watcher = new(accountsDir);
        LiveMonitor monitor = new(watcher, tracker, env.ClientRunning);
        while (!env.Token.IsCancellationRequested)
        {
            if (monitor.Tick() > 0)
            {
                RenderOpen(tracker, output);
            }

            env.Sleep(500);
        }

        // Final full summary on exit; live watch skips no files.
        output.Write(SummaryFormatter.Format(new ReplayResult(tracker.Sessions, tracker.UnattributedCount, [])));
        return 0;
    }

    private static void RenderOpen(SessionTracker tracker, TextWriter output)
    {
        foreach (CharacterSession session in tracker.Open)
        {
            TimeSpan span = session.LastSeen - session.Start;
            MetricSnapshot xp = MetricSnapshot.Compute(session.Stats.Experience, span);
            MetricSnapshot inf = MetricSnapshot.Compute(session.Stats.Influence, span);
            MetricSnapshot tickets = MetricSnapshot.Compute(session.Stats.Tickets, span);
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"[{session.LastSeen:HH:mm:ss}] {SummaryFormatter.Ascii(session.Character)}: xp {xp.Value:0} ({xp.PerHour:0}/hr) | inf {inf.Value:0} ({inf.PerHour:0}/hr) | tickets {tickets.Value:0} ({tickets.PerHour:0}/hr)"));
        }
    }

    private static int Fail(TextWriter error, string message)
    {
        error.WriteLine(message);
        return 1;
    }
}
