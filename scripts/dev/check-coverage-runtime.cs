// check-coverage-runtime.cs - say when the coverage collector will run on an
// older runtime than this repository targets.
//
//   dotnet run scripts/dev/check-coverage-runtime.cs
//
// Why this exists: dotnet-coverage ships tools/net8.0 only, in every version
// including the newest, and its runtimeconfig asks for framework 8.0.0 with
// rollForward Major. So it runs happily on .NET 10 when 8 is absent, and
// prefers 8 as the exact match when 8 is present. .NET 8 leaves support on
// 2026-11-10, after which a host that still carries it quietly keeps collecting
// coverage on an unsupported runtime.
//
// The earlier reading of this - that the collector requires a runtime nothing
// declares - was wrong, and the wrong version would have had CI install .NET 8
// deliberately. The tool's own roll-forward policy is what governs, so the
// honest statement is about preference, not need.
//
// It derives the expectation rather than carrying a date table: the SDK major
// in global.json is what this repository targets, so a collector resolving an
// older major is the thing worth saying out loud. When the SDK pin moves, the
// expectation moves with it.
//
// WARNS, NEVER FAILS. Today's runner image and the maintainer's box both carry
// .NET 8, so failing would redden every pull request to report a condition
// nobody can clear from inside this repository. Making it a gate is a follow-up
// for when the hosts stop shipping 8 - and until then this is a notice, which
// is all it claims to be.
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
    "DOTNET_ROLL_FORWARD=LatestMajor for the collector, to move collection onto a supported runtime. See #291.");
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
