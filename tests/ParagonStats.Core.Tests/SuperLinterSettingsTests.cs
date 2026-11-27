namespace ParagonStats.Core.Tests;

/// <summary>
/// Super-Linter prints only what did not pass (#327). CI and the local runner
/// read the same <c>.github/super-linter.env</c>, so the settings are pinned
/// there; a NOTICE log hides a passing linter's warnings, so a YAML warning
/// fails instead.
/// </summary>
[Collection(DevScriptBuilds.Name)]
public sealed class SuperLinterSettingsTests
{
    [Fact]
    public void The_shared_settings_print_only_what_did_not_pass_and_fail_on_a_yaml_warning()
    {
        Dictionary<string, string> settings = File.ReadAllLines(Path.Join(DevScripts.RepositoryRoot, ".github", "super-linter.env"))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);

        Assert.Equal("NOTICE", settings["LOG_LEVEL"]);
        Assert.Equal("true", settings["SUPPRESS_POSSUM"]);
        Assert.Equal("true", settings["YAML_ERROR_ON_WARNING"]);

        // yamllint reads its rules from the default file name under the rules path.
        Assert.Equal(".github/linters", settings["LINTER_RULES_PATH"]);
        Assert.True(File.Exists(Path.Join(DevScripts.RepositoryRoot, ".github", "linters", ".yaml-lint.yml")));
    }

    [Fact]
    public void The_local_run_keeps_the_shared_log_level_unless_one_is_asked_for()
    {
        (int sharedExit, string shared, string sharedError) = DevScripts.RunScript("run-super-linter.cs", DevScripts.RepositoryRoot, environment: null, "--print-plan");
        Dictionary<string, string> raised = new(StringComparer.Ordinal) { ["SUPER_LINTER_LOG_LEVEL"] = "INFO" };
        (int raisedExit, string asked, string askedError) = DevScripts.RunScript("run-super-linter.cs", DevScripts.RepositoryRoot, raised, "--print-plan");

        Assert.True(sharedExit == 0, shared + sharedError);
        Assert.Contains(".github/super-linter.env", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("LOG_LEVEL=", shared, StringComparison.Ordinal);
        Assert.True(raisedExit == 0, asked + askedError);
        Assert.Contains("LOG_LEVEL=INFO", asked, StringComparison.Ordinal);
    }
}
