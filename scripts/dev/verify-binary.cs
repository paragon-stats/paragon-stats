// verify-binary.cs - run a built paragon-stats binary and check it against golden output.
//
//   dotnet run scripts/dev/verify-binary.cs -- <path-to-binary>
//   dotnet run scripts/dev/verify-binary.cs -- <path-to-binary> --update   (regenerate goldens)
//
// Why this exists (#236): every other test in this repo runs in-process against
// CliRunner.Run. Native AOT trimming failures and CLI surface regressions only
// appear in the *published* artifact, so a fully green suite says nothing about
// what users download. Four releases shipped with no --help before anyone noticed.
//
// Two tiers, and the first one is a hard gate:
//
//   SMOKE   - does the artifact function at all? Launches, exit codes, a version
//             that looks like a version, help that is not empty.
//   GOLDEN  - does its output still match the recorded proof of function?
//
// If SMOKE fails the run stops there and GOLDEN never executes. A golden diff
// taken from a binary that does not launch correctly reports a downstream
// symptom of the upstream break, and the fix goes in against an imagined
// problem. Fix the tier that failed, then the next tier's result means
// something.
//
// The version and the resolved root are machine-specific, so those two banner
// lines are normalised before the diff.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;
using System.Text.RegularExpressions;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: verify-binary <path-to-binary> [--update]");
    return 2;
}

string binary = Path.GetFullPath(args[0]);
bool update = args.Contains("--update");
string fixtures = Path.Combine("tests", "fixtures", "game");
string goldenDir = Path.Combine("tests", "fixtures", "golden");

if (!File.Exists(binary))
{
    Console.Error.WriteLine($"binary not found: {binary}");
    return 2;
}

Directory.CreateDirectory(goldenDir);
List<string> failures = [];

// The banner names the version and the resolved root; both differ per machine
// and per build, and neither is what this check is guarding.
string Normalise(string text) =>
    Regex.Replace(
        Regex.Replace(text, @"(?m)^paragon-stats .+ - Homecoming", "paragon-stats <version> - Homecoming"),
        @"(?m)^reading .+ \(read-only",
        "reading <root> (read-only");

(int Code, string Out, string Err) Run(params string[] arguments)
{
    ProcessStartInfo info = new()
    {
        FileName = binary,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (string argument in arguments)
    {
        info.ArgumentList.Add(argument);
    }

    using Process process = Process.Start(info)!;
    string standardOut = process.StandardOutput.ReadToEnd();
    string standardError = process.StandardError.ReadToEnd();
    process.WaitForExit();
    return (process.ExitCode, standardOut.ReplaceLineEndings("\n"), standardError.ReplaceLineEndings("\n"));
}

void Golden(string name, string actual)
{
    string path = Path.Combine(goldenDir, name);
    if (update)
    {
        File.WriteAllText(path, actual);
        Console.WriteLine($"[UPDATED] {path}");
        return;
    }

    if (!File.Exists(path))
    {
        failures.Add($"{name}: no golden file - run with --update");
        return;
    }

    string expected = File.ReadAllText(path).ReplaceLineEndings("\n");
    if (expected == actual)
    {
        Console.WriteLine($"[OK] {name}");
        return;
    }

    failures.Add($"{name}: output differs from {path}");
    string[] want = expected.Split('\n');
    string[] got = actual.Split('\n');
    for (int i = 0; i < Math.Max(want.Length, got.Length); i++)
    {
        string left = i < want.Length ? want[i] : "<missing>";
        string right = i < got.Length ? got[i] : "<missing>";
        if (left != right)
        {
            failures.Add($"    line {i + 1}:  expected: {left}");
            failures.Add($"    line {i + 1}:  actual:   {right}");
        }
    }
}

void Expect(string what, bool condition, string detail)
{
    if (condition)
    {
        Console.WriteLine($"[OK] {what}");
    }
    else
    {
        failures.Add($"{what}: {detail}");
    }
}

// Ends a tier. Anything failed here means every later tier would be measuring
// the consequences of this break, so stop rather than produce findings nobody
// should act on.
void EndTier(string tier)
{
    if (failures.Count == 0)
    {
        Console.WriteLine($"--- {tier} tier passed ---");
        Console.WriteLine();
        return;
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine($"{tier} tier FAILED ({failures.Count} problem(s)); later tiers not run:");
    foreach (string failure in failures)
    {
        Console.Error.WriteLine($"  {failure}");
    }

    if (string.Equals(tier, "GOLDEN", StringComparison.Ordinal))
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("If the change is intended, regenerate with:");
        Console.Error.WriteLine("  dotnet run scripts/dev/verify-binary.cs -- <path-to-binary> --update");
    }

    Environment.Exit(1);
}

// ---------------------------------------------------------------- SMOKE tier
Console.WriteLine("=== SMOKE ===");

var replay = Run(fixtures);
Expect("batch replay exits 0", replay.Code == 0, $"exit {replay.Code}, stderr: {replay.Err.Trim()}");
Expect("batch replay produces output", replay.Out.Trim().Length > 0, "no stdout");

var help = Run("--help");
Expect("--help exits 0", help.Code == 0, $"exit {help.Code}");
Expect("--help is not empty", help.Out.Trim().Length > 0, "no stdout");

var version = Run("--version");
Expect("--version exits 0", version.Code == 0, $"exit {version.Code}");
Expect(
    "--version prints a version",
    Regex.IsMatch(version.Out.Trim(), @"^\d+\.\d+\.\d+"),
    $"got: {version.Out.Trim()}");

var unknown = Run("--nonsense");
Expect("unknown option exits 2", unknown.Code == 2, $"exit {unknown.Code}");
Expect(
    "unknown option is named, not treated as a path",
    unknown.Err.Contains("unknown option: --nonsense", StringComparison.Ordinal),
    $"stderr: {unknown.Err.Trim()}");

var missing = Run(Path.Combine("no", "such", "directory"));
Expect("missing path exits 1", missing.Code == 1, $"exit {missing.Code}");

EndTier("SMOKE");

// --------------------------------------------------------------- GOLDEN tier
Console.WriteLine("=== GOLDEN ===");

Golden("replay.txt", Normalise(replay.Out));
Golden("help.txt", help.Out);

var shortHelp = Run("-h");
Expect("-h matches --help", shortHelp.Out == help.Out, "-h and --help disagree");

// The fixture carries two synthetic chat lines. They must not reach the output,
// and the captured-line counts must not include them (zero collection).
Expect(
    "chat channels are not collected",
    !replay.Out.Contains("must never be collected", StringComparison.Ordinal),
    "fixture chat text appeared in the output");

EndTier("GOLDEN");

Console.WriteLine(update ? "goldens updated." : "binary verified against goldens.");
return 0;
