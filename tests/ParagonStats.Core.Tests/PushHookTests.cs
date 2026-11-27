using System.Text.Json.Nodes;

namespace ParagonStats.Core.Tests;

/// <summary>
/// When the push hook runs its group (#327). Git lists one ref per line on
/// standard input; a push that only deletes remote branches sends no commits,
/// so there is nothing to build, test or lint. Every other line runs the group,
/// a line the hook cannot read included: a needless run costs minutes, a
/// skipped one an unchecked push.
/// </summary>
public sealed class PushHookTests : IDisposable
{
    private const string Zeros = "0000000000000000000000000000000000000000";
    private const string Commit = "1111111111111111111111111111111111111111";
    private const string Deletion = $"(delete) {Zeros} refs/heads/old {Commit}\n";

    private readonly string _root = Directory.CreateTempSubdirectory("ps-push-").FullName;

    public void Dispose() => DevScripts.Delete(_root);

    [Theory]
    [InlineData("")]
    [InlineData(Deletion)]
    [InlineData(Deletion + Deletion)]
    [InlineData($"(delete) {Zeros}000000000000000000000000 refs/heads/old {Commit}\n")]
    public void The_group_does_not_run_when_there_are_no_commits_to_check(string refs)
    {
        (int exit, string output, string error) = RunHook(refs);

        Assert.True(exit == 0, output + error);
        Assert.Equal(string.Empty, output + error);
    }

    [Theory]
    [InlineData($"refs/heads/new {Commit} refs/heads/new {Zeros}\n")]
    [InlineData(Deletion + $"refs/heads/new {Commit} refs/heads/new {Commit}\n")]
    [InlineData($"(delete) {Zeros}\n")]
    [InlineData($"(delete) {Zeros} refs/heads/old {Commit} extra\n")]
    [InlineData($"(delete) 0 refs/heads/old {Commit}\n")]
    [InlineData("not a ref line\n")]
    public void The_group_runs_quietly_for_any_other_push(string refs)
    {
        (int exit, string output, string error) = RunHook(refs);

        // The group's one task fails, so a run shows as a failure.
        Assert.True(exit != 0, output + error);
        Assert.Contains("no-such-command", output + error, StringComparison.Ordinal);
        Assert.DoesNotContain("[Husky]", output + error, StringComparison.Ordinal);
    }

    private (int Exit, string Output, string Error) RunHook(string refs)
    {
        string repository = DevScripts.NewRepository(_root);
        DevScripts.Git(repository, "config", "core.hooksPath", ".husky");
        DevScripts.Copy(repository, ".husky/pre-push");
        DevScripts.Copy(repository, ".config/dotnet-tools.json");
        DevScripts.Copy(repository, "global.json");

        // husky.sh is generated at install time; the hook only sources it.
        DevScripts.Write(repository, ".husky/_/husky.sh", string.Empty);
        JsonObject task = new() { ["name"] = "marker", ["group"] = "pre-push", ["command"] = "git", ["args"] = new JsonArray("no-such-command") };
        DevScripts.Write(repository, ".husky/task-runner.json", new JsonObject { ["tasks"] = new JsonArray(task) }.ToJsonString());

        return DevScripts.Run(repository, DevScripts.Shell, refs, environment: null, ".husky/pre-push");
    }
}
