// check-encoding.cs - fail on non-ASCII bytes in script/CLI output source.
//
// Run by the Husky pre-commit hook and CI:  dotnet run scripts/dev/check-encoding.cs
//
// Console/CLI output must be encoding-safe (ASCII, or forced UTF-8) so it does not
// mojibake on a legacy-codepage Windows console - see docs/style-guides/encoding.md.
// This scans the tooling scripts and the CLI for any non-ASCII byte (> 0x7E, or a
// control byte other than tab/newline/carriage-return) and reports file:line:col.
// Prose (docs, markdown) is intentionally NOT scanned - the rule is terminal output.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

string[] roots = ["scripts", Path.Combine("src", "ParagonStats.Cli")];
string[] extensions = [".cs", ".py", ".sh", ".ps1"];

List<string> offences = [];
foreach (string root in roots)
{
    if (!Directory.Exists(root))
    {
        continue;
    }

    foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
    {
        if (!extensions.Contains(Path.GetExtension(path)))
        {
            continue;
        }

        byte[] bytes = File.ReadAllBytes(path);
        // Skip a leading UTF-8 BOM: a file-encoding marker, not console-output content.
        int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        int line = 1;
        int col = 0;
        for (int idx = offset; idx < bytes.Length; idx++)
        {
            byte b = bytes[idx];
            if (b == (byte)'\n')
            {
                line++;
                col = 0;
                continue;
            }

            col++;
            bool allowed = b is (byte)'\t' or (byte)'\r' || (b >= 0x20 && b <= 0x7E);
            if (!allowed)
            {
                offences.Add($"{path.Replace('\\', '/')}:{line}:{col}: non-ASCII byte 0x{b:X2}");
            }
        }
    }
}

if (offences.Count > 0)
{
    Console.Error.WriteLine("[FAIL] non-ASCII bytes in script/CLI output source:");
    foreach (string offence in offences)
    {
        Console.Error.WriteLine($"  {offence}");
    }

    Console.Error.WriteLine(
        "Use ASCII in console output, or force UTF-8 (Console.OutputEncoding = Encoding.UTF8). " +
        "See docs/style-guides/encoding.md.");
    return 1;
}

Console.WriteLine("[OK] script/CLI output source is ASCII-clean.");
return 0;
