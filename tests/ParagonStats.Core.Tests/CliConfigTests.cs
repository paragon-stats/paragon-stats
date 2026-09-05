using ParagonStats.Core.Config;
using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

/// <summary>Game-location config: first-launch prompt, re-prompt on failed check, explicit path wins; watch loop wiring.</summary>
public sealed class CliConfigTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ps-config-").FullName;

    private string ConfigPath => Path.Join(_root, "config", "config.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string GameRoot(string name = "game")
    {
        string dir = Path.Join(_root, name, "accounts", "acct", "Logs");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Join(dir, "chatlog 2026-08-31.txt"),
            "2026-08-31 08:00:00 Welcome to City of Heroes, Nova!\n2026-08-31 08:10:00 You gain 10 experience.\n");
        return Path.Join(_root, name);
    }

    [Fact]
    public void Store_round_trips_and_treats_missing_or_malformed_as_first_launch()
    {
        AppConfigStore store = new(ConfigPath);
        Assert.Null(store.LoadGameRoot()); // missing file

        Assert.True(store.TrySaveGameRoot(@"C:\Games\Homecoming"));
        Assert.Equal(@"C:\Games\Homecoming", store.LoadGameRoot());

        File.WriteAllText(ConfigPath, "{not json");
        Assert.Null(store.LoadGameRoot()); // malformed = first launch, never throws
    }

    [Fact]
    public void Resolve_accepts_game_root_or_accounts_shaped_dir_and_rejects_the_rest()
    {
        string game = GameRoot();
        Assert.Equal(Path.Join(game, "accounts"), AppConfigStore.ResolveAccountsDir(game));
        Assert.Equal(Path.Join(game, "accounts"), AppConfigStore.ResolveAccountsDir(Path.Join(game, "accounts")));
        Assert.Null(AppConfigStore.ResolveAccountsDir(Path.Join(_root, "nope")));

        string empty = Path.Join(_root, "empty");
        Directory.CreateDirectory(empty);
        Assert.Null(AppConfigStore.ResolveAccountsDir(empty)); // exists but no Logs shape
    }

    [Fact]
    public void First_launch_prompts_saves_and_replays()
    {
        string game = GameRoot();
        using StringWriter output = new();
        using StringWriter error = new();
        using StringReader input = new(game + Environment.NewLine);

        int exit = CliRunner.Run([], output, error, new CliEnvironment { Input = input, ConfigPath = ConfigPath });

        Assert.Equal(0, exit);
        Assert.Contains("First launch", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Nova", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(game, new AppConfigStore(ConfigPath).LoadGameRoot()); // persisted
    }

    [Fact]
    public void Saved_location_is_reused_without_prompting()
    {
        string game = GameRoot();
        new AppConfigStore(ConfigPath).TrySaveGameRoot(game);
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run([], output, error, new CliEnvironment { ConfigPath = ConfigPath });

        Assert.Equal(0, exit);
        Assert.DoesNotContain("enter the Homecoming install location", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Nova", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Failed_directory_check_reprompts_for_a_new_location()
    {
        string game = GameRoot();
        new AppConfigStore(ConfigPath).TrySaveGameRoot(Path.Join(_root, "unplugged-drive"));
        using StringWriter output = new();
        using StringWriter error = new();
        using StringReader input = new(game + Environment.NewLine);

        int exit = CliRunner.Run([], output, error, new CliEnvironment { Input = input, ConfigPath = ConfigPath });

        Assert.Equal(0, exit);
        Assert.Contains("Saved game location has no chatlogs", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(game, new AppConfigStore(ConfigPath).LoadGameRoot()); // refreshed
    }

    [Fact]
    public void No_input_on_first_launch_fails_cleanly()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run([], output, error, new CliEnvironment { ConfigPath = ConfigPath });

        Assert.Equal(1, exit);
        Assert.Contains("no game location provided", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Explicit_path_wins_and_refreshes_the_saved_value()
    {
        string first = GameRoot("first");
        string second = GameRoot("second");
        AppConfigStore store = new(ConfigPath);
        store.TrySaveGameRoot(first);
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run([second], output, error, new CliEnvironment { ConfigPath = ConfigPath });

        Assert.Equal(0, exit);
        Assert.Equal(second, store.LoadGameRoot());
    }

    [Fact]
    public void Watch_renders_live_rates_and_prints_final_summary_on_cancel()
    {
        string game = GameRoot();
        using StringWriter output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        int ticks = 0;

        int exit = CliRunner.Run(["--watch", game], output, error, new CliEnvironment
        {
            ConfigPath = ConfigPath,
            Token = cancellation.Token,
            Sleep = _ =>
            {
                if (++ticks >= 2)
                {
                    cancellation.Cancel();
                }
            },
        });

        Assert.Equal(0, exit);
        string text = output.ToString();
        Assert.Contains("Nova: xp 10", text, StringComparison.Ordinal); // live line with rates
        Assert.Contains("/hr)", text, StringComparison.Ordinal);
        Assert.Contains("sessions 1", text, StringComparison.Ordinal); // final summary
    }

    [Fact]
    public void Watch_sums_all_boxes_into_a_combined_farm_line()
    {
        // The operator's multibox use case: influence gain across every box
        // at once - one line per box plus the combined total.
        string game = GameRoot();
        string second = Path.Join(game, "accounts", "acct2", "Logs");
        Directory.CreateDirectory(second);
        File.WriteAllText(
            Path.Join(second, "chatlog 2026-08-31.txt"),
            "2026-08-31 08:00:00 Welcome to City of Heroes, Luna!\n2026-08-31 08:10:00 You gain 20 influence.\n");
        using StringWriter output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        int ticks = 0;

        int exit = CliRunner.Run(["--watch", game], output, error, new CliEnvironment
        {
            ConfigPath = ConfigPath,
            Token = cancellation.Token,
            Sleep = _ =>
            {
                if (++ticks >= 2)
                {
                    cancellation.Cancel();
                }
            },
        });

        Assert.Equal(0, exit);
        string text = output.ToString();
        Assert.Contains("Nova:", text, StringComparison.Ordinal);
        Assert.Contains("Luna:", text, StringComparison.Ordinal);
        Assert.Contains("[all 2 boxes] xp 10 (60/hr) | inf 20 (120/hr)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Watch_closes_sessions_when_the_client_process_exits()
    {
        string game = GameRoot();
        using StringWriter output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        int ticks = 0;

        int exit = CliRunner.Run(["--watch", game], output, error, new CliEnvironment
        {
            ConfigPath = ConfigPath,
            Token = cancellation.Token,
            ClientRunning = () => ticks < 1, // client exits after the first tick
            Sleep = _ =>
            {
                if (++ticks >= 3)
                {
                    cancellation.Cancel();
                }
            },
        });

        Assert.Equal(0, exit);
        Assert.Contains("sessions 1", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Watch_surfaces_unreadable_files_in_the_final_summary()
    {
        string game = GameRoot();
        string locked = Path.Join(game, "accounts", "acct", "Logs", "chatlog 2026-08-30.txt");
        File.WriteAllText(locked, "2026-08-30 08:00:00 locked\n");
        using FileStream locker = new(locked, FileMode.Open, FileAccess.Write, FileShare.None);
        using StringWriter output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        int ticks = 0;

        int exit = CliRunner.Run(["--watch", game], output, error, new CliEnvironment
        {
            ConfigPath = ConfigPath,
            Token = cancellation.Token,
            Sleep = _ =>
            {
                if (++ticks >= 2)
                {
                    cancellation.Cancel();
                }
            },
        });

        Assert.Equal(0, exit);
        Assert.Contains("watching ", output.ToString(), StringComparison.Ordinal); // startup acknowledgment
        Assert.Contains("skipped (unreadable)", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Watch_rejects_a_file_target_and_two_positionals_are_usage()
    {
        string game = GameRoot();
        string file = Directory.GetFiles(game, "chatlog*.txt", SearchOption.AllDirectories)[0];
        using StringWriter output = new();
        using StringWriter error = new();

        Assert.Equal(1, CliRunner.Run(["--watch", file], output, error, new CliEnvironment { ConfigPath = ConfigPath }));
        Assert.Contains("--watch needs a directory", error.ToString(), StringComparison.Ordinal);

        Assert.Equal(2, CliRunner.Run(["a", "b"], output, error, new CliEnvironment { ConfigPath = ConfigPath }));
        Assert.Contains("usage:", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Default_environment_carries_real_production_wiring()
    {
        CliEnvironment env = new();
        Assert.True(env.ClientRunning()); // safe default: never force-close sessions
        env.Sleep(1); // the real Thread.Sleep
        Assert.EndsWith(Path.Join("paragon-stats", "config.json"), env.ConfigPath, StringComparison.Ordinal);
        Assert.Null(env.Input);
    }

    [Fact]
    public void Production_environment_wires_console_and_the_process_check()
    {
        using CancellationTokenSource cancellation = new();
        CliEnvironment env = CliEnvironment.Production(cancellation.Token);

        Assert.NotNull(env.Input);
        Assert.Equal(cancellation.Token, env.Token);

        // Deliberately NOT asserting the probe's ANSWER. It reports whether a
        // game client is running on THIS machine, so the old assertion passed in
        // CI and failed on a developer's box the moment they were playing - the
        // same environment dependency the goldens were cured of.
        //
        // What IS asserted is the invariant between the two probes, which holds
        // on any machine in any state: they read the same process list, so a
        // positive count and a negative "is anything running" cannot both be
        // true. Assert.NotNull said nothing at all here - both are non-nullable
        // properties with non-null defaults, so the assertion passed even if
        // the production wiring were deleted outright.
        //
        // Both probes are called into locals first, because `&&` short-circuits:
        // written as one expression, a zero count on a machine with no game
        // running meant ClientRunning was never invoked at all. The assertion
        // read as though it exercised both and exercised one - the coverage
        // data is what exposed it.
        int count = env.ClientCount();
        bool running = env.ClientRunning();
        Assert.False(count > 0 && !running);

        // The found-and-dispose path, deterministic on every machine: this
        // test process is always a real running process.
        string self = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        Assert.True(CliEnvironment.AnyRunning(System.Diagnostics.Process.GetProcessesByName(self)));
        Assert.False(CliEnvironment.AnyRunning([]));
    }

    [Fact]
    public void The_client_count_reports_what_is_running_and_says_nothing_when_it_cannot_tell()
    {
        // This test process is always a real running process, so the
        // found-and-dispose path is deterministic on every machine.
        string self = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        Assert.True(CliEnvironment.RunningClients(() => System.Diagnostics.Process.GetProcessesByName(self)) > 0);
        Assert.Equal(0, CliEnvironment.RunningClients(static () => []));

        // Unsure reads as "nothing to say", never as an accusation that a box
        // is misconfigured: the count only ever drives a message (#252).
        Assert.Equal(0, CliEnvironment.RunningClients(static () => throw new InvalidOperationException()));
    }

    [Fact]
    public void The_production_wiring_is_invoked_not_merely_assigned()
    {
        // These are all lambdas. Coverage marks the line covered the instant the
        // assignment runs, so a body nobody calls is invisible to the gate -
        // which is how the shipped binary lost --help for four releases. Calling
        // them is the only thing that proves they do anything.
        using CancellationTokenSource cancellation = new();
        CliEnvironment forced = CliEnvironment.Production("1", cancellation.Token);
        CliEnvironment live = CliEnvironment.Production(forceTui: null, cancellation.Token);

        // The golden frames are 120x12 and TuiHost reserves the last row for the
        // shell's cursor, so a forced run has to report one row more than it
        // paints. Pinned here because a golden that follows the CI runner's
        // window is not a golden.
        Assert.Equal((120, 13), forced.WindowSize());

        // "Falls back to the 120x12 strip when there is no console to ask" - the
        // answer varies with the host, so what is asserted is that it always
        // returns a usable grid rather than a zero or a throw.
        (int width, int height) = live.WindowSize();
        Assert.True(width > 0 && height > 0);

        // A forced run replays a fixture, so the client it models is present:
        // were the stop authority to report otherwise it would close every
        // session and the golden live frame could never show one open.
        Assert.True(forced.ClientRunning());

        // Console.KeyAvailable throws outright when stdin is redirected, which
        // it is under any test host. The guard is the whole point.
        Assert.Null(Record.Exception(() => live.ReadKey()));
    }

    [Fact]
    public void A_forced_run_reads_keys_from_the_pipe_and_treats_end_of_input_as_quit()
    {
        // Stated contract: "Keys are then read from stdin, and end-of-input
        // quits - so piping nothing renders one frame and exits."
        using CancellationTokenSource cancellation = new();
        CliEnvironment forced = CliEnvironment.Production("1", cancellation.Token);

        TextReader original = Console.In;
        try
        {
            Console.SetIn(TextReader.Null);
            Assert.Equal('q', forced.ReadKey());
        }
        finally
        {
            Console.SetIn(original);
        }
    }

    [Fact]
    public void A_forced_run_pins_the_client_count_so_a_golden_never_depends_on_what_else_is_open()
    {
        // Same reason the window size is pinned on a forced run: whether an
        // unrelated process happens to be running must not decide what a golden
        // frame says. Zero means the notice can never fire while replaying a
        // fixture.
        using CancellationTokenSource cancellation = new();

        Assert.Equal(0, CliEnvironment.Production("1", cancellation.Token).ClientCount());
    }

    [Fact]
    public void Typed_invalid_location_reprompts_with_the_typed_path()
    {
        // First launch, user typos a path: the re-prompt names what THEY
        // typed, never claiming it was a saved config value.
        string game = GameRoot();
        string typo = Path.Join(_root, "typo");
        using StringWriter output = new();
        using StringWriter error = new();
        using StringReader input = new(typo + Environment.NewLine + game + Environment.NewLine);

        int exit = CliRunner.Run([], output, error, new CliEnvironment { Input = input, ConfigPath = ConfigPath });

        Assert.Equal(0, exit);
        Assert.Contains($"No chatlogs found at {typo}", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Saved game location", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(game, new AppConfigStore(ConfigPath).LoadGameRoot());
    }

    [Fact]
    public void Unwritable_config_location_warns_and_continues()
    {
        string blocker = Path.Join(_root, "blocker");
        File.WriteAllText(blocker, "a file where the config directory should be");
        string configPath = Path.Join(blocker, "nested", "config.json");
        Assert.False(new AppConfigStore(configPath).TrySaveGameRoot(@"C:\anything"));

        string game = GameRoot();
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run([game], output, error, new CliEnvironment { ConfigPath = configPath });

        Assert.Equal(0, exit); // the replay still runs
        Assert.Contains("could not save the game location", output.ToString(), StringComparison.Ordinal);
    }
}
