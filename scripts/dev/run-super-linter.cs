// run-super-linter.cs - run the CI Super-Linter image locally for lint parity.
//
// Run:  dotnet run scripts/dev/run-super-linter.cs   (wired to the Husky pre-push hook)
//
// Lints the working tree with the SAME super-linter image + config CI uses
// (.github/super-linter.env), so polyglot/hygiene/secret findings surface locally
// before they reach CI. The image tag is pinned and immutable (kept in sync with the
// action in super-linter.yml), so freshness == presence: pull only when it isn't
// cached - no per-push registry round-trip. Docker is required; when unavailable the
// run is skipped, not failed - CI still lints.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;

// Keep the tag in sync with the super-linter action pin in super-linter.yml.
const string Image = "ghcr.io/super-linter/super-linter:v8.7.0";

// Run docker. When capturing, drain BOTH streams before WaitForExit so a child that
// fills the stderr pipe buffer can't deadlock; otherwise inherit stdio (live output).
static int Docker(bool capture, params string[] args)
{
    try
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = capture,
            RedirectStandardError = capture,
            UseShellExecute = false,
        };
        foreach (string arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(info)!;
        if (capture)
        {
            _ = proc.StandardOutput.ReadToEnd();
            _ = proc.StandardError.ReadToEnd();
        }

        proc.WaitForExit();
        return proc.ExitCode;
    }
    catch
    {
        return 127;
    }
}

if (Docker(capture: true, "--version") != 0)
{
    Console.Error.WriteLine(
        "[SKIP] Docker not found - skipping the local Super-Linter pass; CI will still lint. " +
        "Install Docker for local parity (see CONTRIBUTING.md).");
    return 0;
}

if (Docker(capture: true, "image", "inspect", Image) != 0)
{
    Console.Error.WriteLine($"[INFO] {Image} not cached; pulling ...");
    if (Docker(capture: false, "pull", Image) != 0)
    {
        Console.Error.WriteLine($"[SKIP] could not pull {Image} - skipping; CI will still lint.");
        return 0;
    }
}

string repo = Directory.GetCurrentDirectory();
Console.Error.WriteLine("[INFO] running Super-Linter locally (RUN_LOCAL) ...");
int code = Docker(
    capture: false,
    "run", "--rm",
    "-e", "RUN_LOCAL=true",
    "-e", "DEFAULT_BRANCH=origin/main",  // match CI's diff base (super-linter.yml)
    "--env-file", ".github/super-linter.env",
    "-v", $"{repo}:/tmp/lint",
    Image);

if (code != 0)
{
    Console.Error.WriteLine("[FAIL] Super-Linter reported findings - fix them before pushing.");
}

return code;
