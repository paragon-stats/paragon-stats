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
        writer.Write(Encoding.UTF8.GetBytes("first\r\nsecond-partial"));
        writer.Flush();

        using ChatLogTailer tailer = new(path);
        Assert.Equal(["first\r"], tailer.Poll());

        writer.Write(Encoding.UTF8.GetBytes(" done\r\n"));
        writer.Flush();
        Assert.Equal(["second-partial done\r"], tailer.Poll());
        Assert.Empty(tailer.Poll());
    }

    [Fact]
    public void Tailer_reassembles_multibyte_chars_split_across_polls()
    {
        string path = LogPath();
        byte[] line = Encoding.UTF8.GetBytes("café\n");
        using FileStream writer = new(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        writer.Write(line, 0, 4); // splits the two-byte e-acute in half
        writer.Flush();

        using ChatLogTailer tailer = new(path);
        Assert.Empty(tailer.Poll());

        writer.Write(line, 4, line.Length - 4);
        writer.Flush();
        Assert.Equal(["café"], tailer.Poll());
    }

    [Fact]
    public void Tailer_restarts_from_the_top_on_truncation()
    {
        string path = LogPath();
        File.WriteAllText(path, "old line one\nold line two\n");
        using ChatLogTailer tailer = new(path);
        Assert.Equal(2, tailer.Poll().Count);

        File.WriteAllText(path, "fresh\n"); // shorter than what was read
        Assert.Equal(["fresh"], tailer.Poll());
    }

    [Fact]
    public void Watcher_attaches_files_created_after_it_started()
    {
        using LogWatcher watcher = new(_root);
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
            using LogWatcher watcher = new(_root);
            Assert.Empty(watcher.Poll());
        }

        using LogWatcher retry = new(_root);
        Assert.Single(retry.Poll());
    }

    [Fact]
    public void Daily_rollover_continues_the_same_session()
    {
        File.WriteAllText(LogPath(name: "chatlog 2026-08-30.txt"), "2026-08-30 23:50:00 Welcome to City of Heroes, Nova!\n");
        using LogWatcher watcher = new(_root);
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
    public void Client_exit_closes_every_open_session_at_its_last_line()
    {
        File.WriteAllText(LogPath(), "2026-08-31 08:00:00 Welcome to City of Heroes, Nova!\n2026-08-31 08:10:00 You gain 10 experience.\n");
        bool running = true;
        using LogWatcher watcher = new(_root);
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
