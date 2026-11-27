using System.Text.RegularExpressions;

namespace ParagonStats.Core.Tests;

/// <summary>
/// The local Super-Linter runner (#327): it runs the image CI's action runs,
/// and it lints from a linked worktree as well as from a plain checkout.
/// </summary>
[Collection(DevScriptBuilds.Name)]
public sealed partial class SuperLinterRunnerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ps-runner-").FullName;

    [GeneratedRegex(@"ghcr\.io/super-linter/super-linter:(?<version>v\d+\.\d+\.\d+)@sha256:[0-9a-f]{64}", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RunnerImage { get; }

    [GeneratedRegex(@"uses: super-linter/super-linter@[0-9a-f]{40}\s+#\s*(?<version>v\d+\.\d+\.\d+)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ActionPin { get; }

    public void Dispose() => DevScripts.Delete(_root);

    [Fact]
    public void The_local_image_is_the_version_the_ci_action_runs()
    {
        string runner = File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, "scripts", "dev", "run-super-linter.cs"));
        string workflow = File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, ".github", "workflows", "super-linter.yml"));

        Match image = RunnerImage.Match(runner);
        Match action = ActionPin.Match(workflow);

        Assert.True(image.Success, "no digest-pinned image in the runner");
        Assert.True(action.Success, "no commit-pinned action with a version comment in the workflow");
        Assert.Equal(action.Groups["version"].Value, image.Groups["version"].Value);
    }

    [Fact]
    public void A_linked_worktree_is_mounted_through_its_shared_git_directory()
    {
        string main = DevScripts.NewRepository(_root);
        DevScripts.Commit(main);
        string linked = Path.Join(_root, "linked");
        DevScripts.Git(main, "worktree", "add", "--quiet", linked);
        string gitDir = Directory.GetDirectories(Path.Join(main, ".git", "worktrees")).Single();

        (int exit, string plan, string error) = DevScripts.RunScript("run-super-linter.cs", linked, environment: null, "--print-plan");

        Assert.True(exit == 0, plan + error);
        Assert.Contains($"{Path.Join(main, ".git")}:/git-common", plan, StringComparison.Ordinal);
        Assert.Contains($"{Path.Join(gitDir, "super-linter.gitfile")}:/tmp/lint/.git:ro", plan, StringComparison.Ordinal);
        Assert.Contains("GIT_CONFIG_KEY_0=safe.directory", plan, StringComparison.Ordinal);
        Assert.Contains("GIT_CONFIG_VALUE_0=*", plan, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Join(gitDir, "super-linter.gitfile")), "--print-plan must write nothing");
    }

    [Fact]
    public void A_plain_checkout_mounts_only_the_working_tree()
    {
        string repository = DevScripts.NewRepository(_root);

        (int exit, string plan, string error) = DevScripts.RunScript("run-super-linter.cs", repository, environment: null, "--print-plan");

        Assert.True(exit == 0, plan + error);
        Assert.Contains($"{repository}:/tmp/lint", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("/git-common", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("GIT_CONFIG_", plan, StringComparison.Ordinal);
    }
}
