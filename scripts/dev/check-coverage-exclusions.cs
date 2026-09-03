// check-coverage-exclusions.cs - keep the two coverage exclusion lists in step.
//
//   dotnet run scripts/dev/check-coverage-exclusions.cs
//
// Why this exists: coverage is excluded in two places, in two vocabularies, and
// nothing kept them in step.
//
//   build.yml  reportgenerator -classfilters:"-Namespace.Type;..."   (type names)
//   sonar.yml  /d:sonar.coverage.exclusions="**/File.cs,..."          (path globs)
//
// They drifted. VirtualTerminal was filtered out of the local gate - documented,
// deliberate, its failure paths need a host that refuses the P/Invoke - but the
// Sonar list never learned about it, so Sonar counted all 20 of its lines as
// uncovered new code and reported 93.6% while the local gate read 100%. Two
// tools telling different stories about the same decision.
//
// The check is deliberately narrow: it maps a class filter to the file the type
// must live in and asserts the glob is present, and vice versa. It does NOT
// judge whether an exclusion is justified - that argument belongs in review.
//
// The mapping holds because StyleCop's SA1649 already requires a file to be
// named after the type it declares. One type, one file, one glob.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Text.RegularExpressions;

string buildWorkflow = Path.Combine(".github", "workflows", "build.yml");
string sonarWorkflow = Path.Combine(".github", "workflows", "sonar.yml");

// Entries that legitimately live on one side only. Each needs a reason, so the
// list cannot quietly become the place drift goes to hide.
Dictionary<string, string> classFilterOnly = new(StringComparer.Ordinal)
{
    ["System.Text.RegularExpressions.Generated.*"] = "generated regex internals: no source file to glob",
};

Dictionary<string, string> sonarOnly = new(StringComparer.Ordinal)
{
    ["scripts/**"] = "tooling scripts are not in the coverage report at all",
    ["**/Program.cs"] = "entry point: excluded from coverage, not a filtered type",
};

List<string> problems = [];

string Read(string path)
{
    if (!File.Exists(path))
    {
        problems.Add($"{path}: not found");
        return string.Empty;
    }

    return File.ReadAllText(path);
}

string build = Read(buildWorkflow);
string sonar = Read(sonarWorkflow);
if (problems.Count > 0)
{
    foreach (string missing in problems)
    {
        Console.Error.WriteLine($"[FAIL] {missing}");
    }

    return 2;
}

Match classFilters = Regex.Match(build, @"-classfilters:""(?<list>[^""]*)""");
Match coverageExclusions = Regex.Match(sonar, @"sonar\.coverage\.exclusions=""(?<list>[^""]*)""");

if (!classFilters.Success)
{
    Console.Error.WriteLine($"[FAIL] {buildWorkflow}: no -classfilters: setting found.");
    return 2;
}

if (!coverageExclusions.Success)
{
    Console.Error.WriteLine($"[FAIL] {sonarWorkflow}: no sonar.coverage.exclusions setting found.");
    return 2;
}

// A class filter is "-Some.Namespace.Type"; the leading '-' means exclude.
string[] filtered = classFilters.Groups["list"].Value
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Where(entry => entry.StartsWith('-'))
    .Select(entry => entry[1..])
    .ToArray();

string[] excluded = coverageExclusions.Groups["list"].Value
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToArray();

foreach (string type in filtered)
{
    if (classFilterOnly.ContainsKey(type))
    {
        continue;
    }

    string expected = $"**/{type[(type.LastIndexOf('.') + 1)..]}.cs";
    if (!excluded.Contains(expected, StringComparer.Ordinal))
    {
        problems.Add(
            $"{type} is filtered out of the local coverage gate but not out of Sonar's."
            + $" Add {expected} to sonar.coverage.exclusions in {sonarWorkflow},"
            + $" or drop the class filter in {buildWorkflow}.");
    }
}

foreach (string glob in excluded)
{
    if (sonarOnly.ContainsKey(glob))
    {
        continue;
    }

    Match file = Regex.Match(glob, @"^\*\*/(?<type>[A-Za-z0-9_]+)\.cs$");
    if (!file.Success)
    {
        problems.Add($"{glob}: not a **/Type.cs glob and not in the sonar-only list. Add a reason there if it belongs.");
        continue;
    }

    string type = file.Groups["type"].Value;
    if (!filtered.Any(entry => entry.EndsWith('.' + type, StringComparison.Ordinal) || string.Equals(entry, type, StringComparison.Ordinal)))
    {
        problems.Add(
            $"{glob} is excluded from Sonar's coverage but the type is still measured by the local gate."
            + $" Add a -{type} class filter in {buildWorkflow}, or drop the Sonar exclusion.");
    }
}

if (problems.Count > 0)
{
    Console.Error.WriteLine("[FAIL] the two coverage exclusion lists disagree:");
    foreach (string problem in problems)
    {
        Console.Error.WriteLine($"  {problem}");
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine("Both tools must tell the same story about what is not measured.");
    return 1;
}

Console.WriteLine($"[OK] coverage exclusions agree ({filtered.Length} class filter(s), {excluded.Length} Sonar glob(s)).");
return 0;
