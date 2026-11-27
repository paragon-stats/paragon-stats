// run-super-linter.cs - run the CI Super-Linter image locally for lint parity.
//
// Run:  dotnet run scripts/dev/run-super-linter.cs   (wired to the Husky pre-push hook)
//       dotnet run scripts/dev/run-super-linter.cs -- --print-plan   (print the docker arguments only)
//
// Lints the working tree with the SAME super-linter image + config CI uses
// (.github/super-linter.env), so polyglot/hygiene/secret findings surface locally
// before they reach CI. The image is pinned by digest (kept in sync with the action in
// super-linter.yml), so freshness == presence: pull only when it isn't cached - no
// per-commit registry round-trip. Docker is required; when unavailable the run is
// skipped, not failed - CI still lints. SUPER_LINTER_LOG_LEVEL (for example INFO) is
// passed as LOG_LEVEL over the shared file's NOTICE, for a full log without editing it.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;

// Keep in sync with the super-linter action pin in super-linter.yml. The digest is what
// binds the version; the tag is retained only so the reference is readable.
const string Image =
    "ghcr.io/super-linter/super-linter:v8.7.0@sha256:c05768164eed53bac7c82aade7a14a76955206d4962cd41be97118db96fa5996";

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

List<string> run =
[
    "run", "--rm",
    "-e", "RUN_LOCAL=true",
    "-e", "DEFAULT_BRANCH=origin/main",  // match CI's diff base (super-linter.yml)
    "--env-file", ".github/super-linter.env",
];

// docker gives -e precedence over --env-file, so a level asked for here outranks the shared NOTICE.
string? logLevel = Environment.GetEnvironmentVariable("SUPER_LINTER_LOG_LEVEL");
if (!string.IsNullOrEmpty(logLevel))
{
    run.AddRange(["-e", $"LOG_LEVEL={logLevel}"]);
}

run.AddRange(["-v", $"{Directory.GetCurrentDirectory()}:/tmp/lint", Image]);
if (args.Contains("--print-plan"))
{
    Console.WriteLine(string.Join(' ', run));
    return 0;
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

Console.Error.WriteLine("[INFO] running Super-Linter locally (RUN_LOCAL) ...");
int code = Docker(capture: false, [.. run]);

if (code != 0)
{
    Console.Error.WriteLine("[FAIL] Super-Linter reported findings - fix them before pushing.");
}

return code;
