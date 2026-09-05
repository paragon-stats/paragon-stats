using ParagonStats.Core.Sessions;
using ParagonStats.Core.Stats;
using ParagonStats.Core.Tui;

namespace ParagonStats.Core.Tests;

/// <summary>
/// The loop, driven synchronously the way the watch tests drive theirs: a
/// scripted key source and a Sleep that counts ticks and cancels. No threads,
/// no timing assumptions.
/// </summary>
public sealed class TuiHostTests : IDisposable
{
    /// <summary>
    /// The scripted environments hand their token to a closure, so the source
    /// cannot be scoped to a `using` inside the helper. Held and disposed with
    /// the fixture instead.
    /// </summary>
    private readonly List<CancellationTokenSource> _cancellations = [];

    public void Dispose()
    {
        foreach (CancellationTokenSource cancellation in _cancellations)
        {
            cancellation.Dispose();
        }
    }

    [Fact]
    public void The_menu_paints_first_and_q_quits()
    {
        using StringWriter output = new();
        Queue<char> keys = new(['q']);

        int exit = TuiHost.Run(output, Empty, "0.5.0", "root", Env(keys, ticks: 5));

        Assert.Equal(0, exit);
        Assert.Contains("[1]  Live stats", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("CHARACTER", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_that_means_nothing_leaves_you_on_the_screen_you_were_on()
    {
        // ScreenResult.Stay is documented as "Nothing happened, or the key meant
        // nothing here. Keep painting this screen." Nothing exercised that,
        // so the host's do-nothing branch was the one untested outcome of a
        // keypress.
        using StringWriter output = new();
        Queue<char> keys = new(['z', 'q']);

        // Counting the frames is the half the first version of this test
        // missed. Asserting only "menu present, live absent" cannot tell
        // "stayed on the menu, then quit" from "quit immediately on z":
        // mutating the host to treat Stay as Quit left it green, and left the
        // whole TuiHost suite green. Stay's contract is "keep painting this
        // screen", so the painting is the thing to count - two frames, one
        // before each key.
        int painted = 0;
        Snapshot Advance()
        {
            painted++;
            return Snapshot.Capture([], 0);
        }

        TuiHost.Run(output, Advance, "0.5.0", "root", Env(keys, ticks: 5));

        Assert.Equal(2, painted);
        Assert.Empty(keys);

        // Still the menu, and never the live readout, despite a key being read.
        Assert.Contains("[1]  Live stats", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("CHARACTER", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Pressing_one_switches_to_the_live_readout()
    {
        using StringWriter output = new();
        Queue<char> keys = new(['1', 'q']);

        TuiHost.Run(output, Empty, "0.5.0", "root", Env(keys, ticks: 5));

        Assert.Contains("CHARACTER", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_help_screen_is_reachable_and_returns_to_the_menu()
    {
        using StringWriter output = new();
        Queue<char> keys = new(['2', 'm', 'q']);

        TuiHost.Run(output, Empty, "0.5.0", "root", Env(keys, ticks: 8));

        string painted = output.ToString();
        Assert.Contains("exit codes", painted, StringComparison.Ordinal);
        Assert.Contains("[1]  Live stats", painted, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancellation_ends_the_loop_without_a_keypress()
    {
        using StringWriter output = new();

        int exit = TuiHost.Run(output, Empty, "0.5.0", "root", Env(new Queue<char>(), ticks: 3));

        Assert.Equal(0, exit);
        Assert.NotEqual(string.Empty, output.ToString(), StringComparer.Ordinal);
    }

    [Fact]
    public void It_repaints_every_tick_so_the_clock_advances_while_nobody_types()
    {
        // The old watch loop only printed when a line arrived, so a quiet
        // minute looked frozen.
        using StringWriter output = new();
        int painted = 0;

        TuiHost.Run(
            output,
            () =>
            {
                painted++;
                return Snapshot.Capture([], 0);
            },
            "0.5.0",
            "root",
            Env(new Queue<char>(), ticks: 4));

        Assert.Equal(4, painted);
    }

    [Fact]
    public void A_terminal_run_paints_ansi_and_a_piped_one_paints_plain()
    {
        // Showing the readout and emitting escapes are separate questions: a
        // piped run wants the first without the second, which is how CI proves
        // the screens through the published binary.
        Readout readout = new("0.5.0", "root", Snapshot.Capture([], 0));
        string escape = ((char)27).ToString();

        string ansi = TuiHost.Paint(new MenuScreen(), readout, new CliEnvironment { Ansi = true });
        string plain = TuiHost.Paint(new MenuScreen(), readout, new CliEnvironment { Ansi = false });

        Assert.StartsWith(escape + "[H", ansi, StringComparison.Ordinal);
        Assert.DoesNotContain(escape, plain, StringComparison.Ordinal);
        Assert.Contains("[1]  Live stats", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void The_frame_follows_the_window_rather_than_a_fixed_layout()
    {
        Readout readout = new("0.5.0", "root", Snapshot.Capture([], 0));

        string narrow = TuiHost.Paint(new MenuScreen(), readout, Sized(60, 10));
        string wide = TuiHost.Paint(new MenuScreen(), readout, Sized(160, 20));

        // The rule spans the frame, so its length reports the width actually used.
        Assert.Contains(new string('-', 60), narrow, StringComparison.Ordinal);
        Assert.Contains(new string('-', 160), wide, StringComparison.Ordinal);
        Assert.True(Lines(wide) > Lines(narrow));
    }

    [Fact]
    public void A_tiny_window_still_renders_rather_than_throwing()
    {
        Readout readout = new("0.5.0", "root", Snapshot.Capture([], 0));

        string painted = TuiHost.Paint(new LiveScreen(), readout, Sized(1, 1));

        Assert.NotEqual(string.Empty, painted, StringComparer.Ordinal);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        using StringWriter output = new();
        CliEnvironment env = Env(new Queue<char>(), 1);

        Assert.Throws<ArgumentNullException>(() => TuiHost.Run(null!, Empty, "v", "r", env));
        Assert.Throws<ArgumentNullException>(() => TuiHost.Run(output, null!, "v", "r", env));
        Assert.Throws<ArgumentNullException>(() => TuiHost.Run(output, Empty, "v", "r", null!));
    }

    private static Snapshot Empty() => Snapshot.Capture([], 0);

    private static int Lines(string text) => text.Split('\n').Length;

    private static CliEnvironment Sized(int width, int height) => new()
    {
        WindowSize = () => (width, height),
    };

    private CliEnvironment Env(Queue<char> keys, int ticks, bool interactive = false)
    {
        CancellationTokenSource cancellation = new();
        _cancellations.Add(cancellation);
        int elapsed = 0;
        return new CliEnvironment
        {
            Interactive = interactive,
            Token = cancellation.Token,
            ReadKey = () => keys.Count > 0 ? keys.Dequeue() : null,
            WindowSize = static () => (120, 12),
            Sleep = _ =>
            {
                if (++elapsed >= ticks)
                {
                    cancellation.Cancel();
                }
            },
        };
    }
}
