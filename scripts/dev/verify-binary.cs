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
string Normalise(string text)
{
    // Batch banner: version and resolved root differ per machine and per build.
    text = Regex.Replace(text, @"(?m)^paragon-stats .+ - Homecoming", "paragon-stats <version> - Homecoming");
    text = Regex.Replace(text, @"(?m)^reading .+ \(read-only", "reading <root> (read-only");

    // Text UI chrome: same two facts, different layout. The version is padded
    // against the frame width, so the whole header line is rewritten rather
    // than patched, or a longer pre-release version shifts every column.
    text = Regex.Replace(text, @"(?m)^ paragon-stats \S+(\s+)(\w+)\s+read-only \*$", " paragon-stats <version>$1$2   <read-only>");
    text = Regex.Replace(text, @"(?m)^ .*?   (no live sessions|\d+ live)   unattributed (\d+)$", " <root>   $1   unattributed $2");
    return text;
}

(int Code, string Out, string Err) Run(params string[] arguments) => RunPiped(null, null, arguments);

(int Code, string Out, string Err) RunPiped(string? stdin, (string Name, string Value)[]? environment, params string[] arguments)
{
    ProcessStartInfo info = new()
    {
        FileName = binary,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = stdin is not null,
    };
    foreach (string argument in arguments)
    {
        info.ArgumentList.Add(argument);
    }

    foreach ((string name, string value) in environment ?? [])
    {
        info.Environment[name] = value;
    }

    using Process process = Process.Start(info)!;
    if (stdin is not null)
    {
        process.StandardInput.Write(stdin);
        process.StandardInput.Close();
    }

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

var helpFlag = Run("--help");
Expect("--help exits 0", helpFlag.Code == 0, $"exit {helpFlag.Code}");
Expect("--help is not empty", helpFlag.Out.Trim().Length > 0, "no stdout");

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

// The text UI, driven through the published binary. Without the force switch
// this is unprovable: redirected output falls back to the batch summary, which
// is the path that was already covered while the interactive one shipped
// broken. End-of-input quits, so piping nothing renders one frame and exits.
// Written to temp, never into the repo: a generated file under tests/fixtures
// is one `git add -A` away from being committed.
string tuiConfig = Path.Combine(Path.GetTempPath(), "paragon-stats-verify-config.json");
File.WriteAllText(tuiConfig, "{\"GameRoot\":\"" + fixtures.Replace("\\", "/") + "\"}");
(string, string)[] tuiEnvironment =
[
    ("PARAGON_STATS_TUI", "1"),
    ("PARAGON_STATS_CONFIG", tuiConfig),
];

// The watcher is a LIVE monitor: LogWatcher.Discover only attaches to files
// written within the attach window, so the fixture must look freshly written
// or no tailer attaches and every frame renders an empty readout. That is not
// a hypothetical - it is why these goldens passed on a developer machine and
// failed in CI, where checkout had just stamped the files. Touch them here so
// both environments read the same thing, and so the frames prove ingestion
// rather than proving the fixture was too old to open.
foreach (string log in Directory.EnumerateFiles(fixtures, "*.txt", SearchOption.AllDirectories))
{
    File.SetLastWriteTimeUtc(log, DateTime.UtcNow);
}

var menu = RunPiped(string.Empty, tuiEnvironment);
Expect("the text UI launches and exits", menu.Code == 0, $"exit {menu.Code}, stderr: {menu.Err.Trim()}");
Expect(
    "the menu offers every destination",
    menu.Out.Contains("[1]  Live stats", StringComparison.Ordinal)
        && menu.Out.Contains("[q]  Quit", StringComparison.Ordinal),
    "menu entries missing");

var live = RunPiped("1q", tuiEnvironment);
Expect("the live readout is reachable", live.Code == 0, $"exit {live.Code}");
Expect(
    "the live readout paints its columns",
    live.Out.Contains("CHARACTER", StringComparison.Ordinal) && live.Out.Contains("XP/hr", StringComparison.Ordinal),
    "live columns missing");

var help = RunPiped("2q", tuiEnvironment);
Expect(
    "help inside the frame keeps its promises",
    help.Out.Contains("never collected", StringComparison.Ordinal)
        && help.Out.Contains("0 success", StringComparison.Ordinal),
    "help promises missing");

EndTier("SMOKE");

// --------------------------------------------------------------- GOLDEN tier
Console.WriteLine("=== GOLDEN ===");

Golden("replay.txt", Normalise(replay.Out));
Golden("help.txt", helpFlag.Out);
Golden("tui-menu.txt", Normalise(menu.Out));
Golden("tui-live.txt", Normalise(live.Out));
Golden("tui-help.txt", Normalise(help.Out));

var shortHelp = Run("-h");
Expect("-h matches --help", shortHelp.Out == helpFlag.Out, "-h and --help disagree");

// The fixture carries two synthetic chat lines. They must not reach the output,
// and the captured-line counts must not include them (zero collection).
Expect(
    "chat channels are not collected",
    !replay.Out.Contains("must never be collected", StringComparison.Ordinal),
    "fixture chat text appeared in the output");

EndTier("GOLDEN");

Console.WriteLine(update ? "goldens updated." : "binary verified against goldens.");
return 0;
