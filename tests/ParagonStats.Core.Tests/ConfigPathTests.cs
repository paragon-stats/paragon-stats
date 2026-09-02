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
    public void The_default_environment_resolves_a_usable_path()
    {
        // The property default runs the real reader; prove it produces
        // something rooted rather than throwing or returning empty.
        CliEnvironment env = new();

        Assert.False(string.IsNullOrWhiteSpace(env.ConfigPath));
        Assert.True(Path.IsPathRooted(env.ConfigPath));
    }
}
