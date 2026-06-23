// check-commit-message.cs - validate a commit message follows Conventional Commits,
// and enforce that release-triggering types touch product code.
//
// Run by the Husky.Net commit-msg hook and the commitlint CI workflow:
//   dotnet run scripts/git/check-commit-message.cs -- <commit-msg-file> [changed-path ...]
//
// Format: `type(scope): subject` (Conventional Commits). Additionally, a feat/fix commit
// must change at least one file under src/ (product code): feat/fix bump the version under
// start-at-patch, so tooling/docs/test/CI changes must use ci/chore/docs/test/build/
// refactor and never trigger a release. CI tooling, not shipped product code: exempt from
// the solution-wide StyleCop/Meziantou analyzers + warnings-as-errors.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Text.RegularExpressions;

string[] types =
    ["feat", "fix", "docs", "chore", "ci", "refactor", "test", "perf", "style", "build", "revert"];
var subject = new Regex(@"^(?<type>" + string.Join("|", types) + @")(?:\([\w.\-/ ]+\))?!?: .+");
string[] allowedPrefixes = ["Merge ", "Revert ", "fixup!", "squash!"];

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: check-commit-message <commit-msg-file> [changed-path ...]");
    return 2;
}

string[] lines = [.. File.ReadAllLines(args[0])
    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))];

if (lines.Length == 0)
{
    Console.Error.WriteLine("commit message is empty");
    return 1;
}

string first = lines[0];
if (allowedPrefixes.Any(prefix => first.StartsWith(prefix, StringComparison.Ordinal)))
{
    return 0;
}

var match = subject.Match(first);
if (!match.Success)
{
    Console.Error.WriteLine(
        "Commit subject is not a Conventional Commit:\n" +
        $"  {first}\n" +
        $"Expected: type(scope): subject   (types: {string.Join(", ", types)})");
    return 1;
}

// Release-triggering types must change product code. The caller (the commit-msg hook and
// CI) passes the changed paths; skip this check when none are provided.
string type = match.Groups["type"].Value;
string[] changedPaths = [.. args.Skip(1)];
if (type is "feat" or "fix" && changedPaths.Length > 0)
{
    bool touchesSrc = changedPaths.Any(path =>
        path.Replace('\\', '/').StartsWith("src/", StringComparison.Ordinal));
    if (!touchesSrc)
    {
        Console.Error.WriteLine(
            $"'{type}:' changes nothing under src/ (product code):\n" +
            $"  {first}\n" +
            "feat/fix bump the version (start-at-patch) and must touch src/. Use\n" +
            "ci:/chore:/docs:/test:/build:/refactor: for tooling, docs, tests, or CI.");
        return 1;
    }
}

return 0;
