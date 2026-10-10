// check-coverage-runtime.cs - say when the coverage collector will run on an
// older runtime than this repository targets.
//
//   dotnet run scripts/dev/check-coverage-runtime.cs
//
// Why this exists: a tool's own runtimeconfig decides which runtime it gets,
// not this repository. dotnet-coverage asks for an older framework with
// rollForward Major, so it rolls forward where that major is absent and takes
// it as the exact match where it is present. A host carrying the older major
// keeps collecting coverage on it once it leaves support, and says nothing.
//
// Both halves of the comparison are read rather than written down: the
// framework the collector asks for comes from the package restored by
// .config/dotnet-tools.json, and the major this repository targets comes from
// global.json. When either pin moves, the expectation moves with it.
//
// WARNS, NEVER FAILS. The runner image carries the older runtime, so failing
// would redden every pull request over a condition no change in this
// repository can clear. CONTRIBUTING.md records the acceptance.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

// The framework the collector asks for. Read from the restored package rather
// than hardcoded, so a bump that finally retargets the tool is noticed here.
string manifestPath = Path.Combine(".config", "dotnet-tools.json");
if (!File.Exists(manifestPath))
{
    Console.WriteLine("[??] no tool manifest; run from the repository root");
    return 0;
}

using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
if (!manifest.RootElement.GetProperty("tools").TryGetProperty("dotnet-coverage", out JsonElement tool))
{
    Console.WriteLine("[OK] dotnet-coverage is not in the manifest; nothing to check.");
    return 0;
}

string toolVersion = tool.GetProperty("version").GetString()!;
string packaged = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".nuget", "packages", "dotnet-coverage", toolVersion, "tools");
int wanted = Directory.Exists(packaged)
    ? Directory.GetDirectories(packaged)
        .Select(d => Path.GetFileName(d))
        .Select(t => Regex.Match(t, @"^net(\d+)\.0$"))
        .Where(m => m.Success)
        .Select(m => int.Parse(m.Groups[1].Value))
        .DefaultIfEmpty(0)
        .Min()
    : 0;

if (wanted == 0)
{
    Console.WriteLine($"[??] cannot read the target framework of dotnet-coverage {toolVersion}; restore first");
    return 0;
}

// What this repository targets.
using JsonDocument global = JsonDocument.Parse(File.ReadAllText("global.json"));
string sdk = global.RootElement.GetProperty("sdk").GetProperty("version").GetString()!;
int targeted = int.Parse(sdk.Split('.')[0]);

// What the host offers. An exact match for the tool's framework wins over any
// roll-forward, so the presence of that major is the whole question.
(int code, string listed) = Run("dotnet", "--list-runtimes");
if (code != 0)
{
    Console.WriteLine("[??] could not list runtimes");
    return 0;
}

int[] installed = Regex.Matches(listed, @"^Microsoft\.NETCore\.App (\d+)\.", RegexOptions.Multiline)
    .Select(m => int.Parse(m.Groups[1].Value))
    .Distinct()
    .OrderBy(v => v)
    .ToArray();

Console.WriteLine($"collector targets net{wanted}.0, repository targets {targeted}.x, host has {string.Join(", ", installed)}");

if (!installed.Contains(wanted))
{
    Console.WriteLine($"[OK] no {wanted}.x runtime here, so the collector rolls forward to {installed.Max()}.x.");
    return 0;
}

if (wanted >= targeted)
{
    Console.WriteLine($"[OK] the collector resolves {wanted}.x, which is not older than what this repository targets.");
    return 0;
}

Console.WriteLine(
    $"::warning::The coverage collector will run on .NET {wanted}.x because this host has it, " +
    $"while the repository targets {targeted}.x. Remove the {wanted}.x runtime, or set " +
    "DOTNET_ROLL_FORWARD=LatestMajor for the collector, to move collection onto a supported runtime.");
Console.WriteLine($"[!!] collector prefers .NET {wanted}.x over the targeted {targeted}.x (notice only; this check never fails)");
return 0;

static (int Code, string Output) Run(string file, string arguments)
{
    ProcessStartInfo info = new(file, arguments)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    using Process process = Process.Start(info)!;
    string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    process.WaitForExit();
    return (process.ExitCode, output);
}
