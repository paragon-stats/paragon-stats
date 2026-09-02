using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

/// <summary>
/// The config path is the seam that makes the no-argument and first-launch
/// flows testable. Without it those flows can only be exercised by writing
/// over a real user's config, which is why they went untested and a binary
/// shipped with no --help (#245).
/// </summary>
public sealed class ConfigPathTests
{
    [Fact]
    public void An_override_wins_over_the_appdata_default()
    {
        string configured = Path.Join("D:", "elsewhere", "config.json");

        string path = CliEnvironment.DefaultConfigPath(name =>
            string.Equals(name, CliEnvironment.ConfigPathVariable, StringComparison.Ordinal) ? configured : null);

        Assert.Equal(configured, path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Absent_or_blank_falls_back_to_appdata(string? configured)
    {
        // A variable set to whitespace is a mistake, not an instruction to
        // write the config to a directory named " ".
        string path = CliEnvironment.DefaultConfigPath(_ => configured);

        Assert.EndsWith(Path.Join("paragon-stats", "config.json"), path, StringComparison.Ordinal);
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            path,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_documented_variable_is_read()
    {
        List<string> asked = [];

        CliEnvironment.DefaultConfigPath(name =>
        {
            asked.Add(name);
            return null;
        });

        Assert.Equal([CliEnvironment.ConfigPathVariable], asked);
    }

    [Fact]
    public void A_null_reader_is_rejected() =>
        Assert.Throws<ArgumentNullException>(() => CliEnvironment.DefaultConfigPath(null!));

    [Fact]
    public void The_production_environment_is_safe_to_construct_and_poll()
    {
        // Constructed on every run, including ones that never enter the text
        // UI and ones with redirected streams, so none of its wiring may throw
        // just by being built or asked.
        using CancellationTokenSource cancellation = new();
        CliEnvironment env = CliEnvironment.Production(cancellation.Token);

        Assert.Null(env.ReadKey()); // stdin is redirected under the test host
        (int width, int height) = env.WindowSize();
        Assert.True(width > 0 && height > 0);
        Assert.NotNull(env.Input);
        Assert.False(env.Interactive); // redirected output is never interactive
    }

    [Fact]
    public void The_console_size_falls_back_rather_than_throwing()
    {
        (int width, int height) = CliEnvironment.ConsoleSize();

        // Either a real console or the 120x12 strip, never zero - a frame with
        // no cells would throw on construction.
        Assert.True(width > 0);
        Assert.True(height > 0);
    }

    [Fact]
    public void A_real_console_size_is_used_as_given()
    {
        Assert.Equal((80, 25), CliEnvironment.ConsoleSize(static () => (80, 25)));
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(80, 0)]
    [InlineData(-1, -1)]
    public void A_nonsense_console_size_falls_back_to_the_strip(int width, int height)
    {
        // A zero would build a frame with no cells and throw on construction.
        Assert.Equal((120, 12), CliEnvironment.ConsoleSize(() => (width, height)));
    }

    [Fact]
    public void A_console_that_refuses_to_answer_falls_back_to_the_strip()
    {
        // No console attached: asking throws rather than returning anything.
        Assert.Equal((120, 12), CliEnvironment.ConsoleSize(static () => throw new IOException("no console")));
        Assert.Throws<ArgumentNullException>(() => CliEnvironment.ConsoleSize(null!));
    }

    [Fact]
    public void The_default_environment_resolves_a_usable_path()
    {
        // The property default runs the real reader; prove it produces
        // something rooted rather than throwing or returning empty.
        CliEnvironment env = new();

        Assert.False(string.IsNullOrWhiteSpace(env.ConfigPath));
        Assert.True(Path.IsPathRooted(env.ConfigPath));
    }
}
