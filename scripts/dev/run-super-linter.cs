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
    "ghcr.io/super-linter/super-linter:v9.0.0@sha256:7620fb6f07a1908b0642d9ddc4f49da7d9f2b9497bcafee4d3d151d1e5567c0b";
const string Workspace = "/tmp/lint";
const string GitCommon = "/git-common";

// Run docker. When capturing, read stderr on another task while stdout is drained, so a
// child that fills either pipe buffer cannot block on the one nobody is reading; otherwise
// inherit stdio (live output).
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
            Task<string> error = proc.StandardError.ReadToEndAsync();
            _ = proc.StandardOutput.ReadToEnd();
            _ = error.GetAwaiter().GetResult();
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

string root = Directory.GetCurrentDirectory();
run.AddRange(["-v", $"{root}:{Workspace}"]);

// A linked worktree's .git is a file naming a host path the container cannot see. Mount the
// shared git directory, and mount over the workspace's .git a file that names its container
// path; the mounted repository belongs to another user inside the container, hence safe.directory.
string dotGit = Path.Join(root, ".git");
(string Path, string Content)? gitFile = null;
if (File.Exists(dotGit))
{
    string gitDir = Path.GetFullPath(File.ReadAllText(dotGit).Split("gitdir:", 2)[1].Trim(), root);
    string common = Path.GetFullPath(File.ReadAllText(Path.Join(gitDir, "commondir")).Trim(), gitDir);
    string gitFilePath = Path.Join(gitDir, "super-linter.gitfile");

    // No trailing newline: Super-Linter keeps it as part of the path it reads.
    gitFile = (gitFilePath, $"gitdir: {GitCommon}/{Path.GetRelativePath(common, gitDir).Replace('\\', '/')}");
    run.AddRange(
    [
        "-v", $"{common}:{GitCommon}",
        "-v", $"{gitFilePath}:{Workspace}/.git:ro",
        "-e", "GIT_CONFIG_COUNT=1",
        "-e", "GIT_CONFIG_KEY_0=safe.directory",
        "-e", "GIT_CONFIG_VALUE_0=*",
    ]);
}

run.Add(Image);
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

if (gitFile is { } linked)
{
    File.WriteAllText(linked.Path, linked.Content);
}

Console.Error.WriteLine("[INFO] running Super-Linter locally (RUN_LOCAL) ...");
int code = Docker(capture: false, [.. run]);

if (code != 0)
{
    Console.Error.WriteLine("[FAIL] Super-Linter reported findings - fix them before pushing.");
}

return code;
