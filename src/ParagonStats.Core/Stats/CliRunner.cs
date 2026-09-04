using System.Globalization;
using System.Reflection;

using ParagonStats.Core.Config;
using ParagonStats.Core.Logging;
using ParagonStats.Core.Sessions;
using ParagonStats.Core.Tui;

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
    private const string Usage = "usage: paragon-stats [--watch] [chatlog-file-or-game-directory]";

    /// <summary>
    /// How many consecutive frames a client/log mismatch must hold before it is
    /// shown. Both loops run at 500ms, so this is about a minute - long enough
    /// for a login flow to finish and short enough to catch a character switch
    /// while the operator is still at the keyboard.
    /// </summary>
    private const int QuietFramesBeforeNotice = 120;

    /// <summary>
    /// Everything the parser accepts. Anything else opening with '-' is a
    /// typo, not a path: treating "--wathc" as a directory reported "no
    /// chatlog files found" and looked like a broken tool rather than a
    /// misspelling (#238).
    /// </summary>
    private static readonly string[] KnownOptions = ["--watch", "--help", "-h", "--version"];

    /// <summary>
    /// The whole CLI surface, one array entry per output line so the console
    /// never sees a stray line ending. The deliverable is a single portable
    /// exe with no installer and no man page, so this is the only
    /// documentation a user is guaranteed to have in front of them.
    /// </summary>
    private static readonly string[] HelpText =
    [
        "paragon-stats - Homecoming session statistics from your own chat logs",
        string.Empty,
        Usage,
        "       paragon-stats --help | --version",
        string.Empty,
        "arguments:",
        "  chatlog-file-or-game-directory",
        "      A single chatlog file, or a Homecoming install or accounts directory.",
        "      Omit it to use the saved location; you are prompted on first launch.",
        string.Empty,
        "options:",
        "  --watch      Follow the logs live with a rolling readout. Ctrl+C once for",
        "               the session summary, twice to quit.",
        "  --help, -h   Show this help and exit.",
        "  --version    Show the version and exit.",
        string.Empty,
        "exit codes:",
        "  0  success",
        "  1  no chatlogs found, or no game location provided",
        "  2  bad usage",
        string.Empty,
        "This tool only ever reads. It never writes to the game directory, and chat",
        "and other communication channels are never collected - they are recognised",
        "and discarded, never stored.",
    ];

    /// <summary>
    /// Gets the version MinVer stamped at build time. Everything after '+' is
    /// the commit hash, which is noise at a prompt.
    /// </summary>
    private static string Version =>
        typeof(CliRunner).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error) =>
        Run(args, output, error, new CliEnvironment());

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, CliEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(env);

        int? answered = Preflight(args, output, error);
        if (answered is not null)
        {
            return answered.Value;
        }

        bool watch = Given(args, "--watch");
        List<string> positional = [.. args.Where(argument => !KnownOptions.Contains(argument, StringComparer.Ordinal))];
        if (positional.Count > 1)
        {
            Fail(error, Usage);
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

            Banner(output, target);
            return Replay([target], target, output, error);
        }

        string? accounts = ResolveRoot(target, output, error, env);
        if (accounts is null)
        {
            return 1;
        }

        // A no-argument launch on a real terminal is the double-click case, and
        // it gets the text UI. Everything else - an explicit path, --watch, or
        // any redirected run - keeps today's output byte for byte, which is
        // what the golden flows check.
        if (target is null && !watch && env.Interactive)
        {
            return Interactive(accounts, output, env);
        }

        Banner(output, accounts);
        return watch ? Watch(accounts, output, env) : ReplayDirectory(accounts, output, error);
    }

    /// <summary>
    /// The text UI over the live engine. Session-level by ruling: it attaches to
    /// what is happening now rather than replaying history at launch, which is
    /// what made the old no-argument path sit silent for a minute before
    /// printing anything.
    /// </summary>
    private static int Interactive(string accountsDir, TextWriter output, CliEnvironment env)
    {
        SessionTracker tracker = new();
        using LogWatcher watcher = new(accountsDir, SessionTracker.IdleTimeout);
        LiveMonitor monitor = new(watcher, tracker, env.ClientRunning);

        return TuiHost.Run(
            output,
            () =>
            {
                monitor.Tick();
                return Snapshot.Capture(tracker);
            },
            Version,
            accountsDir,
            env,
            SilentBoxes(env, watcher));
    }

    /// <summary>
    /// Says so when a game client is running but nothing of its is being read.
    /// Homecoming stores chat logging per CHARACTER, so a box drops out of the
    /// totals on every character switch - hit three times in one evening of
    /// testing, each time leaving plausible-looking totals a third short (#252).
    /// Silent when the counts agree, and silent when the count is unknown.
    ///
    /// Returns a probe holding its own streak, because a single disagreeing
    /// frame is not evidence: a client sitting at the login or character-select
    /// screen genuinely has no log yet, and accusing it on every launch would
    /// teach the operator to ignore the one message that matters.
    /// </summary>
    private static Func<string?> SilentBoxes(CliEnvironment env, LogWatcher watcher)
    {
        int quiet = 0;
        return () =>
        {
            int clients = env.ClientCount();
            int logging = watcher.AttachedAccounts;
            quiet = clients > logging ? quiet + 1 : 0;
            return quiet >= QuietFramesBeforeNotice
                ? string.Create(CultureInfo.InvariantCulture, $"!! {clients} clients, {logging} logging - enable Log Chat")
                : null;
        };
    }

    /// <summary>
    /// The argument-only surface, answered before anything touches the disk so
    /// it works on a machine with no logs and no saved configuration. Returns
    /// the exit code when the run is already answered, null to carry on.
    /// </summary>
    private static int? Preflight(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (Given(args, "--help") || Given(args, "-h"))
        {
            foreach (string line in HelpText)
            {
                output.WriteLine(line);
            }

            return 0;
        }

        if (Given(args, "--version"))
        {
            output.WriteLine(Version);
            return 0;
        }

        string? unknown = args.FirstOrDefault(argument =>
            argument.StartsWith('-') && !KnownOptions.Contains(argument, StringComparer.Ordinal));
        if (unknown is null)
        {
            return null;
        }

        Fail(error, $"unknown option: {unknown}");
        Fail(error, Usage);
        return 2;
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
        string[] files = [.. ChatLogTree.EnumerateLogs(directory)];

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

        // --watch is the documented live mode and the only one when output is
        // redirected, so it needs the same warning the text UI gets: without it
        // this path hit the identical #252 failure with nothing said. Printed
        // when the message changes rather than every frame, because a rolling
        // log must not be drowned by a repeating banner.
        Func<string?> notice = SilentBoxes(env, watcher);
        string? announced = null;
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

            string? quiet = notice();
            if (quiet is not null && !string.Equals(quiet, announced, StringComparison.Ordinal))
            {
                output.WriteLine(quiet);
            }

            announced = quiet;
            env.Sleep(500);
        }

        output.Write(SummaryFormatter.Format(new ReplayResult(
            tracker.Sessions,
            tracker.UnattributedCount,
            [.. watcher.Unreadable],
            new UnattributedValue(
                tracker.UnattributedExperience,
                tracker.UnattributedInfluence,
                tracker.UnattributedTickets))));
        return 0;
    }

    private static void Save(AppConfigStore store, string gameRoot, TextWriter output)
    {
        if (!store.TrySaveGameRoot(gameRoot))
        {
            output.WriteLine("warning: could not save the game location; you will be prompted again next launch");
        }
    }

    /// <summary>
    /// Printed once the root is resolved, so it names the directory actually
    /// about to be read rather than what was typed. States the read-only and
    /// zero-collection contracts up front: the user is pointing a tool at
    /// their own game install and deserves to know what it will do before it
    /// does it.
    /// </summary>
    private static void Banner(TextWriter output, string root)
    {
        output.WriteLine($"paragon-stats {Version} - Homecoming session statistics");
        output.WriteLine($"reading {root} (read-only; chat channels are never collected)");
        output.WriteLine("quick start: --watch for a live readout, --help for all options");
    }

    private static bool Given(IReadOnlyList<string> args, string option) =>
        args.Contains(option, StringComparer.Ordinal);

    private static void Fail(TextWriter error, string message) => error.WriteLine(message);
}
