// bootstrap.cs - set up and verify the local dev environment (cross-platform).
//
// Run:  dotnet run scripts/dev/bootstrap.cs              (set up)
//       dotnet run scripts/dev/bootstrap.cs -- --verify  (check only, no changes)
//
// Restores the .NET local tools and installs the Husky.Net git hooks, then verifies
// the toolchain (git + signed-commit config; signing is REQUIRED and blocks).
// Idempotent - `dotnet tool restore` and `dotnet husky install` are safe to re-run.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;

bool verify = args.Contains("--verify");

static int Run(string file, string fileArgs)
{
    try
    {
        using var proc = Process.Start(new ProcessStartInfo(file, fileArgs) { UseShellExecute = false })!;
        proc.WaitForExit();
        return proc.ExitCode;
    }
    catch
    {
        return 127;
    }
}

static string Capture(string file, string fileArgs)
{
    try
    {
        using var proc = Process.Start(new ProcessStartInfo(file, fileArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;
        string output = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit();
        return output;
    }
    catch
    {
        return string.Empty;
    }
}

static bool SigningConfigured() =>
    Capture("git", "config --get commit.gpgsign").Equals("true", StringComparison.OrdinalIgnoreCase);

static bool HooksInstalled() =>
    Capture("git", "config --get core.hooksPath").Replace('\\', '/').EndsWith(".husky", StringComparison.Ordinal);

if (string.IsNullOrWhiteSpace(Capture("git", "--version")))
{
    Console.Error.WriteLine("[FAIL] git not found on PATH.");
    return 1;
}

if (verify)
{
    List<string> problems = [];
    if (!HooksInstalled())
    {
        problems.Add("Husky.Net hooks not installed - run: dotnet run scripts/dev/bootstrap.cs");
    }

    if (!SigningConfigured())
    {
        problems.Add("commit.gpgsign != true - signed commits are required.");
    }

    if (problems.Count > 0)
    {
        Console.Error.WriteLine("[FAIL] dev environment is not ready:");
        foreach (string problem in problems)
        {
            Console.Error.WriteLine($"  - {problem}");
        }

        return 1;
    }

    Console.WriteLine("[OK] dev environment is correct and up to date.");
    return 0;
}

Console.WriteLine("[INFO] restoring .NET local tools ...");
if (Run("dotnet", "tool restore") != 0)
{
    return 1;
}

Console.WriteLine("[INFO] installing Husky.Net git hooks ...");
if (Run("dotnet", "husky install") != 0)
{
    return 1;
}

// Signed commits are required by the repo ruleset - block until configured.
if (!SigningConfigured())
{
    Console.Error.WriteLine(
        "[FAIL] commit.gpgsign != true - signed commits are required. Configure signing, then re-run.");
    return 1;
}

Console.WriteLine("[OK] dev environment ready.");
return 0;
