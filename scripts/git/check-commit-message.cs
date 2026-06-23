// check-commit-message.cs - validate a commit message follows Conventional Commits.
//
// Run by the Husky.Net commit-msg hook with the commit message file path as the
// single argument. Ported verbatim from the former scripts/check_commit_message.py.
// CI tooling, not shipped product code: exempt from the solution-wide
// StyleCop/Meziantou analyzers + warnings-as-errors. Not part of ParagonStats.sln.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Text.RegularExpressions;

string[] types =
    ["feat", "fix", "docs", "chore", "ci", "refactor", "test", "perf", "style", "build", "revert"];
var subject = new Regex(@"^(?:" + string.Join("|", types) + @")(?:\([\w.\-/ ]+\))?!?: .+");
string[] allowedPrefixes = ["Merge ", "Revert ", "fixup!", "squash!"];

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: check-commit-message <commit-msg-file>");
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

if (!subject.IsMatch(first))
{
    Console.Error.WriteLine(
        "Commit subject is not a Conventional Commit:\n" +
        $"  {first}\n" +
        $"Expected: type(scope): subject   (types: {string.Join(", ", types)})");
    return 1;
}

return 0;
