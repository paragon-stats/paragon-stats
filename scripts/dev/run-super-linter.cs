// run-super-linter.cs - run the CI Super-Linter image locally for lint parity.
//
// Run:  dotnet run scripts/dev/run-super-linter.cs   (wired to the Husky pre-push hook)
//
// Lints the working tree with the SAME super-linter image + config CI uses
// (.github/super-linter.env), so polyglot/hygiene/secret findings surface locally
// before they reach CI. Keeps the image fresh efficiently (registry-digest compare ->
// pull-if-stale -> prune), mirroring scripts/mcp/run_sonarqube_mcp.py. Docker is
// required; when it is unavailable the run is skipped, not failed - CI still lints.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

// Keep the tag in sync with the super-linter action pin in super-linter.yml.
const string Image = "ghcr.io/super-linter/super-linter:v8.7.0";

static (int Code, byte[] Output) Capture(params string[] args)
{
    try
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(info)!;
        using var buffer = new MemoryStream();
        proc.StandardOutput.BaseStream.CopyTo(buffer);
        proc.WaitForExit();
        return (proc.ExitCode, buffer.ToArray());
    }
    catch
    {
        return (127, []);
    }
}

static string CaptureText(params string[] args)
{
    (int code, byte[] output) = Capture(args);
    return code == 0 ? Encoding.UTF8.GetString(output).Trim() : string.Empty;
}

static int Inherit(params string[] args)
{
    try
    {
        var info = new ProcessStartInfo("docker") { UseShellExecute = false };
        foreach (string arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(info)!;
        proc.WaitForExit();
        return proc.ExitCode;
    }
    catch
    {
        return 127;
    }
}

// Repo digest of the locally cached image (the 'sha256:...' after '@'), or empty.
string LocalRepoDigest()
{
    string raw = CaptureText("image", "inspect", "--format", "{{index .RepoDigests 0}}", Image);
    int at = raw.IndexOf('@');
    return at >= 0 ? raw[(at + 1)..] : string.Empty;
}

// Registry manifest digest, computed from the raw manifest without pulling layers.
string RemoteRepoDigest()
{
    (int code, byte[] manifest) = Capture("buildx", "imagetools", "inspect", "--raw", Image);
    return code == 0
        ? "sha256:" + Convert.ToHexString(SHA256.HashData(manifest)).ToLowerInvariant()
        : string.Empty;
}

int Pull()
{
    Console.Error.WriteLine($"[INFO] pulling {Image} ...");
    return Inherit("pull", Image);
}

// Pull only if missing or stale; prune the superseded image. Returns false only when
// the image is absent and cannot be pulled (caller skips the lint).
bool EnsureImageFresh()
{
    string local = LocalRepoDigest();
    if (string.IsNullOrEmpty(local))
    {
        Console.Error.WriteLine($"[INFO] {Image} not cached locally.");
        return Pull() == 0;
    }

    string remote = RemoteRepoDigest();
    if (string.IsNullOrEmpty(remote))
    {
        Console.Error.WriteLine("[WARN] cannot reach the registry; using the cached image.");
        return true;
    }

    if (local == remote)
    {
        return true;
    }

    string oldId = CaptureText("image", "inspect", "--format", "{{.Id}}", Image);
    Console.Error.WriteLine($"[INFO] {Image} is stale.");
    if (Pull() != 0)
    {
        return true;  // pull failed, but a cached image is present - lint with it
    }

    if (!string.IsNullOrEmpty(oldId))
    {
        Capture("rmi", oldId);  // prune the superseded image by id
    }

    return true;
}

if (string.IsNullOrEmpty(CaptureText("--version")))
{
    Console.Error.WriteLine(
        "[SKIP] Docker not found - skipping the local Super-Linter pass; CI will still lint. " +
        "Install Docker for local parity (see CONTRIBUTING.md).");
    return 0;
}

if (!EnsureImageFresh())
{
    Console.Error.WriteLine($"[SKIP] could not obtain {Image} - skipping; CI will still lint.");
    return 0;
}

string repo = Directory.GetCurrentDirectory();
Console.Error.WriteLine("[INFO] running Super-Linter locally (RUN_LOCAL) ...");
int code = Inherit(
    "run", "--rm",
    "-e", "RUN_LOCAL=true",
    "-e", "DEFAULT_BRANCH=main",
    "--env-file", ".github/super-linter.env",
    "-v", $"{repo}:/tmp/lint",
    Image);

if (code != 0)
{
    Console.Error.WriteLine("[FAIL] Super-Linter reported findings - fix them before pushing.");
}

return code;
