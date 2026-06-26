// check-encoding.cs - fail on non-ASCII characters in script/CLI output source.
//
// Run by the Husky pre-commit hook and CI:  dotnet run scripts/dev/check-encoding.cs
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

string[] roots = ["scripts", Path.Combine("src", "ParagonStats.Cli")];
string[] extensions = [".cs", ".py", ".sh", ".ps1"];

List<string> offences = [];
foreach (string root in roots.Where(Directory.Exists))
{
    foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
    {
        if (!extensions.Contains(Path.GetExtension(path)))
        {
            continue;
        }

        // The default reader detects + strips a leading UTF-8 BOM, so the scan sees
        // characters (one offence per visible glyph, editor-accurate column) - not bytes.
        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            for (int col = 0; col < line.Length; col++)
            {
                char c = line[col];
                if (c != '\t' && (c < ' ' || c > '~'))
                {
                    offences.Add($"{path.Replace('\\', '/')}:{i + 1}:{col + 1}: non-ASCII U+{(int)c:X4}");
                }
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
