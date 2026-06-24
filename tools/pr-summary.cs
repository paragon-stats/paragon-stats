// pr-summary.cs — regenerate a PR body's auto-summary block from branch commits.
//
// Run:  dotnet run tools/pr-summary.cs -- --pr <N> --base <branch> [--dry-run]
//       dotnet run tools/pr-summary.cs -- --selftest
//
// Parses conventional commits on the branch, groups them by category, and for
// each linked issue (Closes/Refs #N) pulls the issue's Objective and Acceptance
// Criteria. Multi-issue PRs get per-issue blocks; the result is written between
// the <!-- auto-summary:start/end --> markers in the PR body via `gh`.
// CI tooling, not shipped product code: exempt this one helper from the
// solution-wide StyleCop/Meziantou analyzers + warnings-as-errors (which target
// product code). It is not part of ParagonStats.sln, so build/format never see it.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

const string Start = "<!-- auto-summary:start -->";
const string End = "<!-- auto-summary:end -->";

if (args.Contains("--selftest"))
{
    return SelfTest();
}

string? pr = null, baseBranch = null;
bool dryRun = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--pr" && i + 1 < args.Length) pr = args[++i];
    else if (args[i] == "--base" && i + 1 < args.Length) baseBranch = args[++i];
    else if (args[i] == "--dry-run") dryRun = true;
}

if (pr is null || baseBranch is null)
{
    Console.Error.WriteLine("usage: --pr <N> --base <branch> [--dry-run] | --selftest");
    return 2;
}

string repo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
    ?? throw new InvalidOperationException("GITHUB_REPOSITORY is not set");

var commits = GetCommits($"origin/{baseBranch}");
var issueNumbers = commits.SelectMany(c => c.Issues).Distinct().OrderBy(n => n).ToList();
var issues = issueNumbers.ToDictionary(n => n, n => FetchIssue(repo, n));

string summary = BuildSummary(commits, issues);

string body = Run("gh", $"api repos/{repo}/pulls/{pr} --jq .body");
string updated = ReplaceMarkers(body, summary);

if (dryRun)
{
    Console.WriteLine(updated);
    return 0;
}
if (updated == body)
{
    Console.WriteLine("No auto-summary markers found (or no change); PR body left untouched.");
    return 0;
}

string tmp = Path.GetTempFileName();
try
{
    File.WriteAllText(tmp, updated);
    Run("gh", $"pr edit {pr} --body-file \"{tmp}\"");
}
finally
{
    File.Delete(tmp);
}
Console.WriteLine($"Updated PR #{pr} auto-summary ({commits.Count} commits, {issues.Count} issue(s)).");
return 0;

static List<Commit> GetCommits(string baseRef)
{
    // %h sha | %s subject | %b body, fields split by US (\x1f), records by RS (\x1e).
    string raw = Run("git", $"log {baseRef}..HEAD --no-merges --format=%h%x1f%s%x1f%b%x1e");
    var commits = new List<Commit>();
    foreach (var record in raw.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = record.Trim('\n', '\r').Split('\x1f');
        if (parts.Length < 2) continue;
        commits.Add(ParseCommit(parts[0].Trim(), parts[1].Trim(), parts.Length > 2 ? parts[2] : ""));
    }
    return commits;
}

static Commit ParseCommit(string sha, string subject, string body)
{
    var m = Regex.Match(subject, @"^(?<type>\w+)(?:\((?<scope>[^)]+)\))?!?:\s*(?<summary>.+)$");
    string type = m.Success ? m.Groups["type"].Value.ToLowerInvariant() : "other";
    string? scope = m.Success && m.Groups["scope"].Success ? m.Groups["scope"].Value : null;
    string summary = m.Success ? m.Groups["summary"].Value : subject;
    return new Commit(sha, type, scope, summary, ExtractIssues(subject + "\n" + body));
}

static IReadOnlyList<int> ExtractIssues(string text)
{
    var nums = new SortedSet<int>();
    foreach (Match m in Regex.Matches(text, @"(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?|refs?)\s*:?\s+#(\d+)", RegexOptions.IgnoreCase))
    {
        nums.Add(int.Parse(m.Groups[1].Value));
    }
    return [.. nums];
}

static IssueInfo FetchIssue(string repo, int number)
{
    string title = Run("gh", $"api repos/{repo}/issues/{number} --jq .title").Trim();
    string body = Run("gh", $"api repos/{repo}/issues/{number} --jq .body");
    return new IssueInfo(number, title, Section(body, "Objective"), Section(body, "Acceptance criteria"));
}

// Pull a GitHub issue-form section: a heading (## or ###) whose text matches
// `label`, up to the next heading. Returns "" if absent.
static string Section(string body, string label)
{
    var m = Regex.Match(
        body,
        @"^#{2,3}\s*" + Regex.Escape(label) + @"\s*$(?<content>.*?)(?=^#{1,3}\s|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline);
    return m.Success ? m.Groups["content"].Value.Trim() : "";
}

static string BuildSummary(List<Commit> commits, Dictionary<int, IssueInfo> issues)
{
    var sb = new StringBuilder();
    if (issues.Count >= 2)
    {
        var blocks = new List<string>();
        foreach (var issue in issues.Values.OrderBy(i => i.Number))
        {
            var owned = commits.Where(c => c.Issues.Contains(issue.Number)).ToList();
            var b = new StringBuilder();
            b.AppendLine($"### Issue #{issue.Number} — {issue.Title}");
            if (issue.Objective.Length > 0) b.AppendLine().AppendLine(issue.Objective);
            string grouped = GroupCommits(owned);
            if (grouped.Length > 0) b.AppendLine().Append(grouped);
            if (issue.Acceptance.Length > 0) b.AppendLine().AppendLine("#### ✅ Acceptance Criteria").AppendLine().AppendLine(issue.Acceptance);
            blocks.Add(b.ToString().TrimEnd());
        }
        var orphans = commits.Where(c => c.Issues.Count == 0).ToList();
        if (orphans.Count > 0) blocks.Add("### Other changes\n\n" + GroupCommits(orphans).TrimEnd());
        sb.Append(string.Join("\n\n---\n\n", blocks));
    }
    else
    {
        var single = issues.Values.FirstOrDefault();
        if (single is { Objective.Length: > 0 }) sb.AppendLine(single.Objective).AppendLine();
        sb.Append(GroupCommits(commits).TrimEnd());
        if (single is { Acceptance.Length: > 0 }) sb.AppendLine().AppendLine().AppendLine("#### ✅ Acceptance Criteria").AppendLine().Append(single.Acceptance);
    }
    return sb.ToString().Trim();
}

static string GroupCommits(List<Commit> commits)
{
    (string Type, string Heading)[] order =
    [
        ("feat", "🚀 Features"), ("fix", "🐛 Bug Fixes"), ("perf", "⚡ Performance"),
        ("security", "🔐 Security"), ("refactor", "🚜 Refactor"), ("docs", "📖 Documentation"),
        ("test", "🧪 Testing"), ("ci", "🔧 CI/CD"), ("build", "📦 Build"), ("chore", "🧹 Chores"),
    ];
    var sb = new StringBuilder();
    foreach (var (type, heading) in order)
    {
        var group = commits.Where(c => c.Type == type).ToList();
        if (group.Count == 0) continue;
        sb.AppendLine($"#### {heading}");
        foreach (var c in group)
        {
            string scope = c.Scope is null ? "" : $"**{c.Scope}:** ";
            sb.AppendLine($"- {scope}{c.Summary} ({c.Sha})");
        }
        sb.AppendLine();
    }
    return sb.ToString();
}

static string ReplaceMarkers(string body, string summary)
{
    var rx = new Regex(Regex.Escape(Start) + ".*?" + Regex.Escape(End), RegexOptions.Singleline);
    string replacement = $"{Start}\n{summary}\n{End}";
    return rx.IsMatch(body) ? rx.Replace(body, replacement) : body;
}

static string Run(string file, string args)
{
    var psi = new ProcessStartInfo(file, args)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    using var p = Process.Start(psi) ?? throw new InvalidOperationException($"failed to start {file}");
    string stdout = p.StandardOutput.ReadToEnd();
    string stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();
    if (p.ExitCode != 0) throw new InvalidOperationException($"{file} {args} exited {p.ExitCode}: {stderr}");
    return stdout;
}

static int SelfTest()
{
    var commits = new List<Commit>
    {
        ParseCommit("aaa1111", "feat(core): parse damage events", "Closes #21"),
        ParseCommit("bbb2222", "fix(cli): handle empty log", "Refs #22"),
        ParseCommit("ccc3333", "chore: tidy", ""),
    };
    var issues = new Dictionary<int, IssueInfo>
    {
        [21] = new(21, "Damage parsing", "Parse combat damage lines.", "- [ ] damage totals correct"),
        [22] = new(22, "CLI robustness", "Handle malformed input.", "- [ ] empty file exits 0"),
    };
    string outp = BuildSummary(commits, issues);
    string[] expected =
    [
        "### Issue #21 — Damage parsing", "Parse combat damage lines.", "🚀 Features",
        "**core:** parse damage events (aaa1111)", "✅ Acceptance Criteria", "- [ ] damage totals correct",
        "### Issue #22 — CLI robustness", "### Other changes", "🧹 Chores", "---",
    ];
    var missing = expected.Where(e => !outp.Contains(e, StringComparison.Ordinal)).ToList();
    string roundtrip = ReplaceMarkers($"x\n{Start}\nOLD\n{End}\ny", "NEW");
    if (!roundtrip.Contains("NEW", StringComparison.Ordinal) || roundtrip.Contains("OLD", StringComparison.Ordinal))
        missing.Add("marker-replacement");
    if (missing.Count > 0)
    {
        Console.Error.WriteLine("SELFTEST FAILED, missing: " + string.Join(" | ", missing));
        Console.Error.WriteLine("--- output ---\n" + outp);
        return 1;
    }
    Console.WriteLine("selftest ok");
    return 0;
}

internal sealed record Commit(string Sha, string Type, string? Scope, string Summary, IReadOnlyList<int> Issues);
internal sealed record IssueInfo(int Number, string Title, string Objective, string Acceptance);
