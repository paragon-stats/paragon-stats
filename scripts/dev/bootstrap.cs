// bootstrap.cs - set up and verify the local dev environment (cross-platform).
//
// Run:  dotnet run scripts/dev/bootstrap.cs              (set up)
//       dotnet run scripts/dev/bootstrap.cs -- --verify  (check only, no changes)
//
// Restores the .NET local tools, installs the Husky.Net git hooks and sets the git
// config this repo expects, then verifies the toolchain (git + signed-commit config;
// signing is REQUIRED and blocks).
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

    if (string.IsNullOrWhiteSpace(Capture("docker", "--version")))
    {
        Console.WriteLine(
            "[NOTE] Docker not found - the pre-push Super-Linter pass will be skipped. " +
            "Install Docker for local lint parity with CI.");
    }

    // Advisory, like the Docker note: a clone bootstrapped before this setting existed
    // verifies clean but still asks for --set-upstream on a new branch. Not a failure.
    if (!Capture("git", "config --get push.autoSetupRemote")
            .Equals("true", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine(
            "[NOTE] push.autoSetupRemote is not set - pushing a new branch will ask for " +
            "--set-upstream. Re-run bootstrap without --verify to set it.");
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

// git cannot distribute config to clones, so bootstrap is the only honest place to
// set this for everyone. Without it, pushing a new branch fails with an instruction
// to re-run using --set-upstream. Advisory: never block an otherwise-working setup.
Console.WriteLine("[INFO] enabling push.autoSetupRemote ...");
if (Run("git", "config --local push.autoSetupRemote true") != 0)
{
    Console.Error.WriteLine(
        "[WARN] could not set push.autoSetupRemote - pushing a new branch will ask for --set-upstream.");
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
