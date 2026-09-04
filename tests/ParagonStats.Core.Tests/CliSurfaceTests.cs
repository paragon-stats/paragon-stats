using System.Text.RegularExpressions;

using ParagonStats.Core.Config;
using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

/// <summary>
/// The CLI surface a user meets first: help, version, the start banner, and
/// what happens when they mistype an option. Four releases shipped without
/// any of it because nothing exercised the binary (#238).
/// </summary>
public sealed partial class CliSurfaceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ps-surface-").FullName;

    /// <summary>Only the major.minor.patch head is pinned; MinVer appends pre-release parts between tags.</summary>
    [GeneratedRegex(@"^\d+\.\d+\.\d+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SemanticVersion { get; }

    private string ConfigPath => Path.Join(_root, "config", "config.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_documents_the_whole_surface_and_exits_zero(string option)
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run([option], output, error, Environment());

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, error.ToString());
        string help = output.ToString();

        // Every option the parser accepts must appear, or the help is lying.
        Assert.Contains("usage: paragon-stats", help, StringComparison.Ordinal);
        Assert.Contains("--watch", help, StringComparison.Ordinal);
        Assert.Contains("--help, -h", help, StringComparison.Ordinal);
        Assert.Contains("--version", help, StringComparison.Ordinal);

        // The exit codes the tool actually returns.
        Assert.Contains("0  success", help, StringComparison.Ordinal);
        Assert.Contains("1  no chatlogs found", help, StringComparison.Ordinal);
        Assert.Contains("2  bad usage", help, StringComparison.Ordinal);

        // The two promises worth making in writing.
        Assert.Contains("only ever reads", help, StringComparison.Ordinal);
        Assert.Contains("never collected", help, StringComparison.Ordinal);

        // Console output stays printable ASCII (docs/style-guides/encoding.md);
        // the help text lives in Core, outside check-encoding's scanned roots.
        Assert.All(help, symbol => Assert.True(symbol is '\r' or '\n' || (symbol >= ' ' && symbol <= '~'), "non-printable in help"));
    }

    [Fact]
    public void Help_and_version_answer_without_touching_the_disk()
    {
        // A user's very first run may have no config and no logs; the
        // documentation must still work.
        using StringWriter output = new();
        using StringWriter error = new();

        Assert.Equal(0, CliRunner.Run(["--help"], output, error, Environment()));
        Assert.Equal(0, CliRunner.Run(["--version"], output, error, Environment()));

        Assert.False(File.Exists(ConfigPath));
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Version_prints_the_stamped_version()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run(["--version"], output, error, Environment());

        Assert.Equal(0, exit);
        Assert.Matches(SemanticVersion, output.ToString().Trim());

        // MinVer's build metadata is the commit hash - noise at a prompt.
        Assert.DoesNotContain("+", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_mistyped_option_is_named_rather_than_read_as_a_path()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run(["--wathc"], output, error, Environment());

        Assert.Equal(2, exit);
        Assert.Contains("unknown option: --wathc", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("usage: paragon-stats", error.ToString(), StringComparison.Ordinal);

        // The old behavior: a typo became a directory name and the tool
        // reported no logs, which reads as broken rather than mistyped.
        Assert.DoesNotContain("no chatlog files found", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Two_paths_is_still_a_usage_error()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run(["one", "two"], output, error, Environment());

        Assert.Equal(2, exit);
        Assert.Contains("usage: paragon-stats", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_banner_names_the_resolved_root_and_both_contracts()
    {
        string game = GameRoot();
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run([game], output, error, Environment());

        Assert.Equal(0, exit);
        string[] lines = output.ToString().Split(System.Environment.NewLine);

        Assert.StartsWith("paragon-stats ", lines[0], StringComparison.Ordinal);
        Assert.Contains("Homecoming session statistics", lines[0], StringComparison.Ordinal);

        // The resolved accounts directory, not the argument as typed: the
        // banner has to say what is actually about to be read.
        Assert.Contains(Path.Join(game, "accounts"), lines[1], StringComparison.Ordinal);
        Assert.Contains("read-only", lines[1], StringComparison.Ordinal);
        Assert.Contains("never collected", lines[1], StringComparison.Ordinal);

        Assert.Contains("--help", lines[2], StringComparison.Ordinal);

        // The banner precedes the summary rather than replacing it.
        Assert.Contains("sessions 1", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_banner_names_a_single_file_target_too()
    {
        string game = GameRoot();
        string file = Directory.EnumerateFiles(Path.Join(game, "accounts"), "chatlog*.txt", SearchOption.AllDirectories).Single();
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run([file], output, error, Environment());

        Assert.Equal(0, exit);
        Assert.Contains(file, output.ToString(), StringComparison.Ordinal);
        Assert.Contains("sessions 1", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_no_argument_interactive_launch_opens_the_text_ui()
    {
        // The double-click case. It must not replay history at startup - that
        // silent minute is what made the shipped binary look broken.
        string game = GameRoot();
        Assert.True(new AppConfigStore(ConfigPath).TrySaveGameRoot(game));

        using StringWriter output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        Queue<char> keys = new(['q']);

        int exit = CliRunner.Run([], output, error, new CliEnvironment
        {
            ConfigPath = ConfigPath,
            Interactive = true,
            ReadKey = () => keys.Count > 0 ? keys.Dequeue() : null,
            Token = cancellation.Token,
            Sleep = _ => cancellation.Cancel(),
        });

        Assert.Equal(0, exit);
        Assert.Contains("[1]  Live stats", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("unattributed lines", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_redirected_no_argument_launch_still_prints_the_batch_summary()
    {
        // Interactive defaults false, so pipes and CI keep byte-identical
        // output and every golden flow still holds.
        string game = GameRoot();
        Assert.True(new AppConfigStore(ConfigPath).TrySaveGameRoot(game));

        using StringWriter output = new();
        using StringWriter error = new();

        int exit = CliRunner.Run([], output, error, Environment());

        Assert.Equal(0, exit);
        Assert.Contains("unattributed lines", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("[1]  Live stats", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_client_with_no_log_being_read_is_called_out_on_the_readout()
    {
        // Homecoming stores chat logging per CHARACTER, so a box drops out of
        // the totals on every character switch. Hit three times in one evening
        // of testing, each time leaving plausible-looking totals a third short
        // (#252) - the count was always in hand and always thrown away.
        string game = GameRoot();
        Assert.True(new AppConfigStore(ConfigPath).TrySaveGameRoot(game));

        using StringWriter output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        int frames = 0;

        int exit = CliRunner.Run([], output, error, new CliEnvironment
        {
            ConfigPath = ConfigPath,
            Interactive = true,
            ClientCount = static () => 9,
            ReadKey = static () => null,
            Token = cancellation.Token,

            // Past the debounce, so the notice has had time to earn itself.
            Sleep = _ =>
            {
                if (++frames >= 130)
                {
                    cancellation.Cancel();
                }
            },
        });

        Assert.Equal(0, exit);
        Assert.Contains("9 clients,", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("enable Log Chat", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_client_that_has_only_just_started_is_not_accused()
    {
        // A client at the login or character-select screen genuinely has no log
        // yet. Accusing it on every launch would teach the operator to ignore
        // the one message that matters, so a mismatch has to hold before it is
        // shown.
        string game = GameRoot();
        Assert.True(new AppConfigStore(ConfigPath).TrySaveGameRoot(game));

        using StringWriter output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        int frames = 0;

        int exit = CliRunner.Run([], output, error, new CliEnvironment
        {
            ConfigPath = ConfigPath,
            Interactive = true,
            ClientCount = static () => 9,
            ReadKey = static () => null,
            Token = cancellation.Token,
            Sleep = _ =>
            {
                if (++frames >= 10)
                {
                    cancellation.Cancel();
                }
            },
        });

        Assert.Equal(0, exit);
        Assert.DoesNotContain("clients,", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Watch_says_it_too_rather_than_only_the_text_ui()
    {
        // --watch is the documented live mode and the only one when output is
        // redirected. Wiring the warning to the text UI alone left this path
        // hitting the identical #252 failure with nothing said.
        string game = GameRoot();

        using StringWriter output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        int frames = 0;

        int exit = CliRunner.Run(["--watch", game], output, error, new CliEnvironment
        {
            ConfigPath = ConfigPath,
            ClientCount = static () => 4,
            Token = cancellation.Token,
            Sleep = _ =>
            {
                if (++frames >= 130)
                {
                    cancellation.Cancel();
                }
            },
        });

        Assert.Equal(0, exit);
        Assert.Contains("4 clients,", output.ToString(), StringComparison.Ordinal);

        // Once, not once per frame: a rolling log must not be drowned by a
        // banner that repeats twice a second.
        Assert.Equal(1, Occurrences(output.ToString(), "enable Log Chat"));
    }

    private static int Occurrences(string text, string needle)
    {
        int found = 0;
        for (int seen = text.IndexOf(needle, StringComparison.Ordinal); seen >= 0; seen = text.IndexOf(needle, seen + 1, StringComparison.Ordinal))
        {
            found++;
        }

        return found;
    }

    private CliEnvironment Environment() => new() { ConfigPath = ConfigPath };

    private string GameRoot()
    {
        string dir = Path.Join(_root, "game", "accounts", "acct", "Logs");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Join(dir, "chatlog 2026-08-31.txt"),
            "2026-08-31 08:00:00 Welcome to City of Heroes, Nova!\n2026-08-31 08:10:00 You gain 10 experience.\n");
        return Path.Join(_root, "game");
    }
}
