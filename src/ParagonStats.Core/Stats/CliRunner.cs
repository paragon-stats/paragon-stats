namespace ParagonStats.Core.Stats;

/// <summary>
/// The CLI's whole behavior, in Core so it is testable and coverage-measured:
/// resolve the target to chatlog files, replay, and print the summary.
/// Program.cs stays a one-line shim.
/// </summary>
public static class CliRunner
{
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count != 1)
        {
            error.WriteLine("usage: paragon-stats <chatlog-file-or-directory>");
            return 2;
        }

        string target = args[0];
        string[] files;
        if (Directory.Exists(target))
        {
            files = Directory.GetFiles(target, "chatlog*.txt", SearchOption.AllDirectories);

            // Daily chatlog names sort ordinally into chronological order per account.
            Array.Sort(files, StringComparer.Ordinal);
        }
        else if (File.Exists(target))
        {
            files = [target];
        }
        else
        {
            error.WriteLine($"no chatlog files found at: {target}");
            return 1;
        }

        if (files.Length == 0)
        {
            error.WriteLine($"no chatlog files found at: {target}");
            return 1;
        }

        output.Write(SummaryFormatter.Format(LogReplayer.Replay(files)));
        return 0;
    }
}
