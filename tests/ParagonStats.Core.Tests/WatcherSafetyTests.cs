using ParagonStats.Core.Logging;
using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

/// <summary>
/// Filesystem hostility and long-run safety: the delta review found a watch
/// loop that a deleted file, a junction cycle, or a big tree could kill.
/// </summary>
public sealed class WatcherSafetyTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ps-safety-").FullName;

    private static TimeSpan Window => TimeSpan.FromMinutes(30);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string LogPath(string account, string name = "chatlog 2026-09-01.txt")
    {
        string dir = Path.Join(_root, account, "Logs");
        Directory.CreateDirectory(dir);
        return Path.Join(dir, name);
    }

    [Fact]
    public void A_file_deleted_after_attach_is_detached_and_reported_not_fatal()
    {
        string path = LogPath("acct");
        File.WriteAllText(path, "2026-09-01 12:00:00 You gain 10 experience.\n");
        using LogWatcher watcher = new(_root, Window, discoveryInterval: 100);
        Assert.Single(watcher.Poll());

        File.Delete(path);

        // Every subsequent poll faults on that tailer; the watch survives, and
        // after the retry budget the file is dropped and surfaced.
        for (int i = 0; i < 6; i++)
        {
            Assert.Empty(watcher.Poll());
        }

        Assert.Equal(path, Assert.Single(watcher.Unreadable));
    }

    [Fact]
    public void A_recreated_longer_file_is_picked_up_again()
    {
        // The old length-vs-position rule could never see this: the dead
        // handle's length is frozen, so a recreated longer file read as an
        // ordinary append and the tailer stalled for the rest of the run.
        string path = LogPath("acct");
        File.WriteAllText(path, "2026-09-01 12:00:00 You gain 10 experience.\n");
        using LogWatcher watcher = new(_root, Window, discoveryInterval: 100);
        Assert.Single(watcher.Poll());

        File.Delete(path);
        File.WriteAllText(path, "2026-09-01 13:00:00 You gain 20 experience.\n2026-09-01 13:00:01 You gain 30 experience.\n");

        List<string> seen = [];
        for (int i = 0; i < 4; i++)
        {
            foreach (WatchBatch batch in watcher.Poll())
            {
                seen.AddRange(batch.Lines);
            }
        }

        Assert.Contains("2026-09-01 13:00:01 You gain 30 experience.", seen, StringComparer.Ordinal);
    }

    [Fact]
    public void Chatlogs_outside_the_logs_shape_are_not_watched()
    {
        // Least collection: qualifying a root on the Logs shape but then
        // globbing chatlogs anywhere under it was the inconsistency.
        string stray = Path.Join(_root, "elsewhere");
        Directory.CreateDirectory(stray);
        File.WriteAllText(Path.Join(stray, "chatlog 2026-09-01.txt"), "2026-09-01 12:00:00 You gain 99 experience.\n");
        File.WriteAllText(LogPath("acct"), "2026-09-01 12:00:00 You gain 10 experience.\n");

        using LogWatcher watcher = new(_root, Window, discoveryInterval: 1);
        WatchBatch batch = Assert.Single(watcher.Poll());
        Assert.Equal("acct", batch.Account);
        Assert.Single(batch.Lines);
    }

    [Fact]
    public void Discovery_stops_at_the_depth_and_tailer_bounds()
    {
        // Depth: a chatlog buried deeper than any real install layout.
        string deep = _root;
        for (int i = 0; i < 12; i++)
        {
            deep = Path.Join(deep, "d" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        deep = Path.Join(deep, "Logs");
        Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Join(deep, "chatlog 2026-09-01.txt"), "2026-09-01 12:00:00 You gain 99 experience.\n");

        using LogWatcher shallow = new(_root, Window, discoveryInterval: 1);
        Assert.Empty(shallow.Poll());

        // Count: more live files than a multibox session could ever have.
        for (int i = 0; i <= LogWatcher.MaxTailers; i++)
        {
            string name = "chatlog 2026-09-" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ".txt";
            File.WriteAllText(LogPath("acct", name), "2026-09-01 12:00:00 You gain 10 experience.\n");
        }

        using LogWatcher capped = new(_root, Window, discoveryInterval: 1);
        Assert.Equal(LogWatcher.MaxTailers, capped.Poll().Count);
    }

    [Fact]
    public void An_unwalkable_root_yields_no_logs_instead_of_crashing()
    {
        // A drive pulled mid-scan, or a root that is not a directory at all:
        // discovery degrades to empty rather than killing batch or watch.
        string notADirectory = Path.Join(_root, "a-file.txt");
        File.WriteAllText(notADirectory, "not a directory");

        Assert.Empty(ChatLogTree.EnumerateLogs(notADirectory));
        Assert.Null(ParagonStats.Core.Config.AppConfigStore.ResolveAccountsDir(notADirectory));

        using StringWriter output = new();
        using StringWriter error = new();
        Assert.Equal(1, CliRunner.Run([_root], output, error, new CliEnvironment { ConfigPath = Path.Join(_root, "config.json") }));
        Assert.Contains("no chatlog files found", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unusable_process_check_keeps_sessions_open()
    {
        // Never force-close a live session because the OS would not answer.
        Assert.True(CliEnvironment.ClientProcessRunning(static () => throw new InvalidOperationException("no counters")));
        Assert.True(CliEnvironment.ClientProcessRunning(static () => throw new System.ComponentModel.Win32Exception(5)));
    }
}
