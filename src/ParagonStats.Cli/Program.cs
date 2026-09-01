using ParagonStats.Core.Stats;

if (args.Length != 1)
{
    await Console.Error.WriteLineAsync("usage: paragon-stats <chatlog-file-or-directory>").ConfigureAwait(false);
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
    await Console.Error.WriteLineAsync($"no chatlog files found at: {target}").ConfigureAwait(false);
    return 1;
}

Console.Write(SummaryFormatter.Format(LogReplayer.Replay(files)));
return 0;
