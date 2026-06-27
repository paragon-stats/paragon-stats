// check-commit-message.cs - validate a commit message follows Conventional Commits,
// and enforce that release-triggering types touch product code.
//
// Run by the Husky.Net commit-msg hook and the commitlint CI workflow (branch commits and
// the squash-subject PR title):
//   dotnet run scripts/git/check-commit-message.cs -- <commit-msg-file> [changed-path ...]
//
// Format: `type(scope): subject` (Conventional Commits). Additionally, a feat/fix commit
// must change at least one file under src/ (product code): feat/fix bump the version, so
// tooling/docs/test/CI changes must use ci/chore/docs/test/build/refactor and never trigger
// a release. The type set is the single source of truth in scripts/git/commit-types.txt
// (shared with branch-name.yml). CI tooling, not shipped product code: exempt from the
// solution-wide StyleCop/Meziantou analyzers + warnings-as-errors.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Text.RegularExpressions;

// Single source of truth for the type set, shared with branch-name.yml. Read relative to
// the repo root, where the commit-msg hook and the commitlint workflow both invoke this.
const string TypesPath = "scripts/git/commit-types.txt";
if (!File.Exists(TypesPath))
{
    Console.Error.WriteLine($"type list not found: {TypesPath} (run from the repo root)");
    return 2;
}

string[] types = [.. File.ReadAllLines(TypesPath).Select(line => line.Trim()).Where(line => line.Length > 0)];
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
            "feat/fix bump the version and must touch src/. Use\n" +
            "ci:/chore:/docs:/test:/build:/refactor: for tooling, docs, tests, or CI.");
        return 1;
    }
}

return 0;
