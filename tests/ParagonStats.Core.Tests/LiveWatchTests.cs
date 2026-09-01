using System.Text;

using ParagonStats.Core.Logging;
using ParagonStats.Core.Sessions;

namespace ParagonStats.Core.Tests;

/// <summary>Live-watch behaviors: tailing a file the game still writes, attach-on-poll, client-exit stop.</summary>
public sealed class LiveWatchTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ps-watch-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string LogPath(string account = "acct", string name = "chatlog 2026-08-31.txt")
    {
        string dir = Path.Join(_root, account, "Logs");
        Directory.CreateDirectory(dir);
        return Path.Join(dir, name);
    }

    [Fact]
    public void Tailer_emits_only_complete_lines_and_holds_the_partial()
    {
        string path = LogPath();
        using FileStream writer = new(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        writer.Write(Encoding.UTF8.GetBytes("2026-08-31 08:00:00 first\r\n2026-08-31 08:00:01 second-partial"));
        writer.Flush();

        using ChatLogTailer tailer = new(path);
        Assert.Equal(["2026-08-31 08:00:00 first\r"], tailer.Poll());

        writer.Write(Encoding.UTF8.GetBytes(" done\r\n"));
        writer.Flush();
        Assert.Equal(["2026-08-31 08:00:01 second-partial done\r"], tailer.Poll());
        Assert.Empty(tailer.Poll());
    }

    [Fact]
    public void Tailer_reassembles_multibyte_chars_split_across_polls()
    {
        string path = LogPath();
        byte[] line = Encoding.UTF8.GetBytes("2026-08-31 08:00:00 café\n");
        using FileStream writer = new(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        writer.Write(line, 0, 23); // splits the two-byte e-acute in half
        writer.Flush();

        using ChatLogTailer tailer = new(path);
        Assert.Empty(tailer.Poll());

        writer.Write(line, 23, line.Length - 23);
        writer.Flush();
        Assert.Equal(["2026-08-31 08:00:00 café"], tailer.Poll());
    }

    [Fact]
    public void Tailer_restarts_from_the_top_on_truncation()
    {
        string path = LogPath();
        File.WriteAllText(path, "2026-08-31 08:00:00 old line one\n2026-08-31 08:00:01 old line two\n");
        using ChatLogTailer tailer = new(path);
        Assert.Equal(2, tailer.Poll().Count);

        File.WriteAllText(path, "2026-08-31 09:00:00 fresh\n"); // shorter than what was read
        Assert.Equal(["2026-08-31 09:00:00 fresh"], tailer.Poll());
    }

    [Fact]
    public void Watcher_attaches_files_created_after_it_started()
    {
        using LogWatcher watcher = new(_root, discoveryInterval: 1);
        Assert.Empty(watcher.Poll());

        File.WriteAllText(LogPath(), "2026-08-31 08:00:00 Welcome to City of Heroes, Nova!\n");
        WatchBatch batch = Assert.Single(watcher.Poll());
        Assert.Equal("acct", batch.Account);
        Assert.Single(batch.Lines);
    }

    [Fact]
    public void Watcher_skips_a_locked_file_and_attaches_it_once_released()
    {
        string path = LogPath();
        using (FileStream locker = new(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            locker.Write(Encoding.UTF8.GetBytes("2026-08-31 08:00:00 locked out\n"));
            using LogWatcher watcher = new(_root, discoveryInterval: 1);
            Assert.Empty(watcher.Poll());
        }

        using LogWatcher retry = new(_root, discoveryInterval: 1);
        Assert.Single(retry.Poll());
    }

    [Fact]
    public void Daily_rollover_continues_the_same_session()
    {
        File.WriteAllText(LogPath(name: "chatlog 2026-08-30.txt"), "2026-08-30 23:50:00 Welcome to City of Heroes, Nova!\n");
        using LogWatcher watcher = new(_root, discoveryInterval: 1);
        SessionTracker tracker = new();
        LiveMonitor monitor = new(watcher, tracker, static () => true);
        Assert.Equal(1, monitor.Tick());

        // Midnight: a new day file appears on the SAME account, 15 min later.
        File.WriteAllText(LogPath(name: "chatlog 2026-08-31.txt"), "2026-08-31 00:05:00 You gain 10 experience.\n");
        Assert.Equal(1, monitor.Tick());

        CharacterSession session = Assert.Single(tracker.Sessions);
        Assert.Equal(10, session.Stats.Experience);
    }

    [Fact]
    public void Tailer_refuses_communication_lines_before_materialization()
    {
        // The zero-collection ruling at the reader: a tell never becomes a
        // string - Poll emits only the data lines around it.
        string path = LogPath();
        string content = "2026-08-31 08:00:00 Welcome to City of Heroes, Nova!\n"
            + "2026-08-31 08:00:05 [Tell] :Someone: private words that must never be read\n"
            + "a timestamp-less continuation line of that tell\n"
            + "2026-08-31 08:00:06 You gain 10 experience.\n";
        File.WriteAllText(path, content);

        using ChatLogTailer tailer = new(path);
        IReadOnlyList<string> lines = tailer.Poll();

        Assert.Equal(2, lines.Count);
        Assert.DoesNotContain(lines, static l => l.Contains("private", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, static l => l.Contains("continuation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Tailer_drains_a_newline_less_tail_for_batch_but_refuses_a_comm_tail()
    {
        string path = LogPath();
        File.WriteAllText(path, "2026-08-31 08:00:00 You gain 10 experience.\n2026-08-31 08:00:05 You gain 20 experience.");
        using ChatLogTailer tailer = new(path);
        Assert.Single(tailer.Poll());
        Assert.Equal("2026-08-31 08:00:05 You gain 20 experience.", tailer.Drain());

        string comm = LogPath(name: "chatlog 2026-08-30.txt");
        File.WriteAllText(comm, "2026-08-30 08:00:00 [Tell] :Someone: secret");
        using ChatLogTailer tail2 = new(comm);
        Assert.Empty(tail2.Poll());
        Assert.Null(tail2.Drain());
    }

    [Fact]
    public void Tailer_reopens_a_deleted_and_recreated_file()
    {
        string path = LogPath();
        File.WriteAllText(path, "2026-08-31 08:00:00 You gain 10 experience.\n2026-08-31 08:00:01 You gain 15 experience.\n");
        using ChatLogTailer tailer = new(path);
        Assert.Equal(2, tailer.Poll().Count);

        File.Delete(path);
        File.WriteAllText(path, "2026-08-31 09:00:00 You gain 20 experience.\n");

        Assert.Equal(["2026-08-31 09:00:00 You gain 20 experience."], tailer.Poll());
    }

    [Fact]
    public void Watcher_ignores_files_older_than_the_attach_window()
    {
        string old = LogPath(name: "chatlog 2026-06-01.txt");
        File.WriteAllText(old, "2026-06-01 08:00:00 You gain 10 experience.\n");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-3));

        string live = LogPath();
        File.WriteAllText(live, "2026-08-31 08:00:00 You gain 20 experience.\n");

        using LogWatcher watcher = new(_root, discoveryInterval: 1);
        WatchBatch batch = Assert.Single(watcher.Poll());
        Assert.Single(batch.Lines); // only the recent file attached
    }

    [Fact]
    public void Session_survives_a_persistently_wrong_client_check()
    {
        // Edge-triggered stop authority: a misreading process check (renamed
        // client binary) must not fragment sessions every tick.
        File.WriteAllText(LogPath(), "2026-08-31 08:00:00 Welcome to City of Heroes, Nova!\n");
        using LogWatcher watcher = new(_root, discoveryInterval: 1);
        SessionTracker tracker = new();
        LiveMonitor monitor = new(watcher, tracker, static () => false);

        monitor.Tick(); // the running->gone edge fires exactly once, closing the banner session
        Assert.Empty(tracker.Open);

        // A heartbeat re-opens; with a level-triggered close it would be
        // killed again every tick. Edge-triggered, it survives.
        File.AppendAllText(LogPath(), "2026-08-31 08:00:10 HIT Nova! Your Health power is autohit.\n");
        monitor.Tick();
        File.AppendAllText(LogPath(), "2026-08-31 08:00:20 You gain 10 experience.\n");
        monitor.Tick();
        monitor.Tick();

        CharacterSession open = Assert.Single(tracker.Open);
        Assert.Equal(10, open.Stats.Experience);
        Assert.Equal(2, tracker.Sessions.Count);
    }

    [Fact]
    public void Watcher_survives_a_file_deleted_after_attach()
    {
        string path = LogPath();
        File.WriteAllText(path, "2026-08-31 08:00:00 You gain 10 experience.\n");
        using LogWatcher watcher = new(_root, discoveryInterval: 1);
        Assert.Single(watcher.Poll());

        File.Delete(path); // allowed by the Delete share; the reopen attempt throws and is contained
        Assert.Empty(watcher.Poll());
        Assert.Empty(watcher.Poll());
    }

    [Fact]
    public void Watcher_rejects_a_nonsense_discovery_interval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogWatcher(_root, discoveryInterval: 0));
    }

    [Fact]
    public void Client_exit_closes_every_open_session_at_its_last_line()
    {
        File.WriteAllText(LogPath(), "2026-08-31 08:00:00 Welcome to City of Heroes, Nova!\n2026-08-31 08:10:00 You gain 10 experience.\n");
        bool running = true;
        using LogWatcher watcher = new(_root, discoveryInterval: 1);
        SessionTracker tracker = new();
        LiveMonitor monitor = new(watcher, tracker, () => running);

        monitor.Tick();
        Assert.Single(tracker.Open);

        running = false; // the game client process is gone
        monitor.Tick();
        Assert.Empty(tracker.Open);
        CharacterSession closed = Assert.Single(tracker.Sessions);
        Assert.Equal(new DateTime(2026, 8, 31, 8, 10, 0), closed.LastSeen);
    }
}
