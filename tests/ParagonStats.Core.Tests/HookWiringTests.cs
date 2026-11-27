using System.Text.Json;

namespace ParagonStats.Core.Tests;

/// <summary>
/// How the commit and push hooks run their checks (#327): Husky stops at the
/// first failure, so each group runs its quickest check first, and the hooks
/// leave out Husky's banner so a passing run prints only the checks' own lines.
/// </summary>
public sealed class HookWiringTests
{
    [Fact]
    public void Each_hook_group_runs_its_quickest_check_first()
    {
        Assert.Equal(["check-encoding", "check-coverage-exclusions", "dotnet-format"], Group("pre-commit"));

        // The tests need the build and take seconds; Super-Linter takes about a minute.
        Assert.Equal(["build", "test", "super-linter"], Group("pre-push"));
    }

    [Theory]
    [InlineData("pre-commit")]
    [InlineData("pre-push")]
    public void The_hook_runs_its_group_quietly_and_restores_no_tools(string hook)
    {
        string text = File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, ".husky", hook));

        Assert.Contains($"\ndotnet husky run --quiet --group {hook}\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("tool restore", text, StringComparison.Ordinal);
    }

    internal static JsonElement[] Tasks()
    {
        using JsonDocument runner = JsonDocument.Parse(File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, ".husky", "task-runner.json")));
        return [.. runner.RootElement.GetProperty("tasks").EnumerateArray().Select(task => task.Clone())];
    }

    private static string[] Group(string group) =>
    [
        .. Tasks()
            .Where(task => string.Equals(task.GetProperty("group").GetString(), group, StringComparison.Ordinal))
            .Select(task => task.GetProperty("name").GetString()!),
    ];
}
