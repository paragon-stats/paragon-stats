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

        store.SaveGameRoot(@"C:\Games\Homecoming");
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
        new AppConfigStore(ConfigPath).SaveGameRoot(game);
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
        new AppConfigStore(ConfigPath).SaveGameRoot(Path.Join(_root, "unplugged-drive"));
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
        store.SaveGameRoot(first);
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
}
