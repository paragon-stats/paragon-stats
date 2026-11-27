// check-encoding.cs - fail on non-ASCII characters in script/CLI output source.
//
// Run by CI:               dotnet run scripts/dev/check-encoding.cs              (working tree)
// Run by the commit hook:  dotnet run scripts/dev/check-encoding.cs -- --staged  (staged content)
//
// Console/CLI output must be ASCII so it does not mojibake on a legacy-codepage
// Windows console - see docs/style-guides/encoding.md. Scans the tooling scripts and
// the CLI for non-ASCII characters (codepoint > U+007E, or a control char other than
// tab) and reports file:line:column - one offence per visible character. A leading
// UTF-8 BOM is ignored (a file marker, not output). Prose (docs) is NOT scanned.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;
using System.Text;

string[] roots = ["scripts", Path.Combine("src", "ParagonStats.Cli")];
string[] extensions = [".cs", ".py", ".sh", ".ps1"];

List<(string Path, string Text)> files = [];
if (args.Contains("--staged"))
{
    // The commit judges the index, not the disk. --no-relative and the top-level
    // pathspecs give the same top-level paths from any folder; ACMT keeps files the
    // commit adds, copies, modifies or changes the type of.
    (int code, byte[] listing, string error) = Git(
        "diff", "--cached", "--raw", "--no-abbrev", "--no-renames", "--no-relative", "--diff-filter=ACMT", "-z",
        "--", ":/scripts", ":/src/ParagonStats.Cli");
    if (code != 0)
    {
        Console.Error.WriteLine($"[FAIL] git diff --cached exited {code}; the staged file list is unknown. {error.Trim()}");
        return 2;
    }

    // Raw records alternate ":<old mode> <new mode> <old id> <new id> <status>" and the path.
    // A symlink (120000) holds a target and a submodule (160000) a commit: neither is text.
    string[] records = Encoding.UTF8.GetString(listing).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    for (int i = 0; i + 1 < records.Length; i += 2)
    {
        string[] meta = records[i].Split(' ');
        string path = records[i + 1];
        if (meta[1] is not ("100644" or "100755") || !extensions.Contains(Path.GetExtension(path)))
        {
            continue;
        }

        (int blobCode, byte[] blob, string blobError) = Git("cat-file", "blob", meta[3]);
        if (blobCode != 0)
        {
            Console.Error.WriteLine($"[FAIL] git cannot read the staged content of {path}. {blobError.Trim()}");
            return 2;
        }

        // Read as File.ReadAllText does: a leading BOM is detected and stripped.
        using StreamReader reader = new(new MemoryStream(blob), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        files.Add((path, reader.ReadToEnd()));
    }
}
else
{
    foreach (string root in roots.Where(Directory.Exists))
    {
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (extensions.Contains(Path.GetExtension(path)))
            {
                // The default reader detects + strips a leading UTF-8 BOM.
                files.Add((path.Replace('\\', '/'), File.ReadAllText(path)));
            }
        }
    }
}

// The scan sees characters (one offence per visible glyph, editor-accurate column) - not bytes.
List<string> offences = [];
foreach ((string path, string text) in files)
{
    using StringReader lines = new(text);
    int lineNumber = 0;
    for (string? line = lines.ReadLine(); line is not null; line = lines.ReadLine())
    {
        lineNumber++;
        for (int col = 0; col < line.Length; col++)
        {
            char c = line[col];
            if (c != '\t' && (c < ' ' || c > '~'))
            {
                offences.Add($"{path}:{lineNumber}:{col + 1}: non-ASCII U+{(int)c:X4}");
            }
        }
    }
}

if (offences.Count > 0)
{
    Console.Error.WriteLine("[FAIL] non-ASCII characters in script/CLI output source:");
    foreach (string offence in offences)
    {
        Console.Error.WriteLine($"  {offence}");
    }

    Console.Error.WriteLine("Keep output in these paths ASCII - see docs/style-guides/encoding.md.");
    return 1;
}

Console.WriteLine("[OK] script/CLI output source is ASCII-clean.");
return 0;

// Arguments as a list, so a path with a space stays one argument; output as bytes, as git wrote it.
static (int Code, byte[] Output, string Error) Git(params string[] arguments)
{
    ProcessStartInfo info = new("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (string argument in arguments)
    {
        info.ArgumentList.Add(argument);
    }

    try
    {
        using Process git = Process.Start(info)!;
        Task<string> error = git.StandardError.ReadToEndAsync();
        using MemoryStream output = new();
        git.StandardOutput.BaseStream.CopyTo(output);
        git.WaitForExit();
        return (git.ExitCode, output.ToArray(), error.GetAwaiter().GetResult());
    }
    catch (System.ComponentModel.Win32Exception exception)
    {
        return (127, [], exception.Message);
    }
}
