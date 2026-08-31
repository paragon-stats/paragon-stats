using ParagonStats.Core.Stats;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: paragon-stats <chatlog-file-or-directory>");
    return 2;
}

string target = args[0];
string[] files = Directory.Exists(target)
    ? Directory.GetFiles(target, "chatlog*.txt", SearchOption.AllDirectories)
    : [target];

// Daily chatlog names sort ordinally into chronological order per account.
Array.Sort(files, StringComparer.Ordinal);

if (files.Length == 0 || (!Directory.Exists(target) && !File.Exists(target)))
{
    Console.Error.WriteLine($"no chatlog files found at: {target}");
    return 1;
}

Console.Write(SummaryFormatter.Format(LogReplayer.Replay(files)));
return 0;
