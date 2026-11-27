namespace ParagonStats.Core.Tests;

/// <summary>
/// The commit hook's checks judge what is staged, not the working tree (#327):
/// a file fixed on disk but staged broken must fail, and one staged clean must
/// pass whatever is on disk. CI runs the same scripts without
/// <c>--staged</c>, on the checked-out tree, and that stays as it was.
/// </summary>
[Collection(DevScriptBuilds.Name)]
public sealed class StagedCheckTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ps-staged-").FullName;

    public void Dispose() => DevScripts.Delete(_root);

    [Theory]
    [InlineData("check-encoding")]
    [InlineData("check-coverage-exclusions")]
    public void The_commit_hook_runs_the_check_on_the_staged_content(string name)
    {
        string[] arguments = [.. HookWiringTests.Tasks()
            .Single(task => string.Equals(task.GetProperty("name").GetString(), name, StringComparison.Ordinal))
            .GetProperty("args").EnumerateArray().Select(argument => argument.GetString()!)];

        Assert.Equal(["run", $"scripts/dev/{name}.cs", "--", "--staged"], arguments);
    }

    [Fact]
    public void The_encoding_check_fails_a_staged_non_ascii_character_from_any_folder()
    {
        string repository = DevScripts.NewRepository(_root);
        DevScripts.Write(repository, "scripts/tool.sh", "echo café\n");
        DevScripts.Git(repository, "add", "scripts/tool.sh");
        DevScripts.Write(repository, "scripts/tool.sh", "echo cafe\n");

        // diff.relative would make git list paths from the folder it runs in.
        DevScripts.Git(repository, "config", "diff.relative", "true");

        (int topExit, string topOutput, string topError) = DevScripts.RunScript("check-encoding.cs", repository, environment: null, "--staged");
        (int subExit, _, string subError) = DevScripts.RunScript("check-encoding.cs", Path.Join(repository, "scripts"), environment: null, "--staged");
        (int treeExit, string treeOutput, string treeError) = DevScripts.RunScript("check-encoding.cs", repository, environment: null);

        Assert.True(topExit == 1, topOutput + topError);
        Assert.Contains("scripts/tool.sh:1:9: non-ASCII U+00E9", topError, StringComparison.Ordinal);
        Assert.Equal(1, subExit);
        Assert.Contains("scripts/tool.sh:1:9: non-ASCII U+00E9", subError, StringComparison.Ordinal);
        Assert.True(treeExit == 0, treeOutput + treeError);
    }

    [Fact]
    public void The_encoding_check_passes_staged_ascii_whatever_the_working_tree_holds()
    {
        string repository = DevScripts.NewRepository(_root);
        DevScripts.Write(repository, "scripts/tool.sh", "echo cafe\n");
        DevScripts.Git(repository, "add", "scripts/tool.sh");
        DevScripts.Write(repository, "scripts/tool.sh", "echo café\n");
        DevScripts.Write(repository, "scripts/untracked.sh", "echo café\n");

        (int stagedExit, string stagedOutput, string stagedError) = DevScripts.RunScript("check-encoding.cs", repository, environment: null, "--staged");
        (int treeExit, _, string treeError) = DevScripts.RunScript("check-encoding.cs", repository, environment: null);

        Assert.True(stagedExit == 0, stagedOutput + stagedError);
        Assert.Contains("[OK]", stagedOutput, StringComparison.Ordinal);
        Assert.Equal(1, treeExit);
        Assert.Contains("scripts/tool.sh:1:9", treeError, StringComparison.Ordinal);
    }

    [Fact]
    public void The_coverage_exclusion_check_judges_the_staged_workflows()
    {
        string repository = DevScripts.NewRepository(_root);
        DevScripts.Copy(repository, ".github/workflows/build.yml");
        DevScripts.Copy(repository, ".github/workflows/sonar.yml");
        string build = Path.Join(repository, ".github", "workflows", "build.yml");
        string agreed = File.ReadAllText(build);
        File.WriteAllText(build, agreed.Replace("-classfilters:\"", "-classfilters:\"-ParagonStats.Core.StagedOnly;", StringComparison.Ordinal));
        DevScripts.Git(repository, "add", ".github");
        File.WriteAllText(build, agreed);

        (int stagedExit, string stagedOutput, string stagedError) = DevScripts.RunScript("check-coverage-exclusions.cs", repository, environment: null, "--staged");
        (int treeExit, string treeOutput, string treeError) = DevScripts.RunScript("check-coverage-exclusions.cs", repository, environment: null);

        Assert.True(stagedExit == 1, stagedOutput + stagedError);
        Assert.Contains("ParagonStats.Core.StagedOnly is filtered out of the local coverage gate", stagedError, StringComparison.Ordinal);
        Assert.True(treeExit == 0, treeOutput + treeError);
    }

    [Theory]
    [InlineData("check-encoding.cs", "the staged file list is unknown")]
    [InlineData("check-coverage-exclusions.cs", "not in the index")]
    public void A_staged_check_fails_when_git_cannot_read_the_index(string script, string reason)
    {
        string plain = Directory.CreateDirectory(Path.Join(_root, "not-a-repository")).FullName;

        // Git must not find a repository above the scratch folder either.
        Dictionary<string, string> ceiling = new(StringComparer.Ordinal) { ["GIT_CEILING_DIRECTORIES"] = _root };
        (int exit, string output, string error) = DevScripts.RunScript(script, plain, ceiling, "--staged");

        Assert.True(exit == 2, output + error);
        Assert.Contains("[FAIL]", error, StringComparison.Ordinal);
        Assert.Contains(reason, error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_encoding_check_fails_when_git_cannot_return_a_staged_blob()
    {
        string repository = DevScripts.NewRepository(_root);
        DevScripts.Write(repository, "scripts/tool.sh", "echo cafe\n");
        DevScripts.Git(repository, "add", "scripts/tool.sh");
        string blob = DevScripts.Git(repository, "rev-parse", ":scripts/tool.sh");
        DevScripts.Delete(Path.Join(repository, ".git", "objects", blob[..2]));

        (int exit, string output, string error) = DevScripts.RunScript("check-encoding.cs", repository, environment: null, "--staged");

        Assert.True(exit == 2, output + error);
        Assert.Contains("[FAIL] git cannot read the staged content of scripts/tool.sh", error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_encoding_check_reads_a_staged_path_with_a_space_and_a_non_ascii_character()
    {
        string repository = DevScripts.NewRepository(_root);
        DevScripts.Write(repository, "scripts/my tool é.sh", "echo café\n");
        DevScripts.Git(repository, "add", "scripts");

        (int exit, string output, string error) = DevScripts.RunScript("check-encoding.cs", repository, environment: null, "--staged");

        Assert.True(exit == 1, output + error);
        Assert.Contains("scripts/my tool ", error, StringComparison.Ordinal);
        Assert.Contains(".sh:1:9: non-ASCII U+00E9", error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_encoding_check_judges_a_symlink_staged_as_a_regular_file()
    {
        string repository = DevScripts.NewRepository(_root);
        DevScripts.Write(repository, "target.txt", "target\n");
        DevScripts.Write(repository, "content.txt", "echo café\n");
        string target = DevScripts.Git(repository, "hash-object", "-w", "target.txt");
        string content = DevScripts.Git(repository, "hash-object", "-w", "content.txt");

        // Index entries written directly, so no OS symlink is needed: commit a symlink, then stage a regular file in its place.
        DevScripts.Git(repository, "update-index", "--add", "--cacheinfo", $"120000,{target},scripts/link.sh");
        DevScripts.Commit(repository);
        DevScripts.Git(repository, "update-index", "--cacheinfo", $"100644,{content},scripts/link.sh");

        (int exit, string output, string error) = DevScripts.RunScript("check-encoding.cs", repository, environment: null, "--staged");

        Assert.True(exit == 1, output + error);
        Assert.Contains("scripts/link.sh:1:9: non-ASCII U+00E9", error, StringComparison.Ordinal);
    }
}
