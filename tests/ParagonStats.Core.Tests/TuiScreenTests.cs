using System.Globalization;

using ParagonStats.Core.Sessions;
using ParagonStats.Core.Tui;

namespace ParagonStats.Core.Tests;

/// <summary>
/// Whole-frame assertions. A screen's job is what it puts on screen, so these
/// pin the rendered text rather than poking at internals, and the fixture is
/// driven through <see cref="SessionTracker.Accept(string, string)"/> with raw
/// log lines so the path from text to frame is the real one.
/// </summary>
public sealed class TuiScreenTests
{
    /// <summary>A full-width rule, spelled once rather than 120 transcribed dashes.</summary>
    private static readonly string Rule = new('-', 120);

    private static string Frame(params string[] rows) => string.Join('\n', rows);

    [Fact]
    public void The_live_screen_renders_the_shipped_strip()
    {
        // ALL BOXES spans 02:00:00 - the window from the earliest start to the
        // latest activity - not the 03:20:00 the two rows sum to.
        //
        // Line-per-entry rather than a raw string literal: the frame's own
        // leading space makes a raw literal's lines indent by a non-multiple of
        // four, which editorconfig rejects.
        string expected = Frame(
            " paragon-stats 0.5.0   live                                                                                 read-only *",
            @" C:\Games\Homecoming\accounts   2 live   unattributed 0",
            Rule,
            " CHARACTER          ACCOUNT       CLOCK          XP         INF TICKETS      XP/hr     INF/hr",
            " Laser - ALT F4     mrlaser    02:00:00   1,200,000   4,000,000     312    600,000  2,000,000",
            " Fixture Scrapper   mrlaser2   01:20:00     118,004     386,120      50     88,503    289,590",
            Rule,
            " ALL BOXES                     02:00:00   1,318,004   4,386,120     362    659,002  2,193,060",
            string.Empty,
            string.Empty,
            Rule,
            " [m] menu   [h] help   [q] quit");

        Assert.Equal(expected, Render(new LiveScreen()), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void The_menu_renders_every_destination()
    {
        string expected = Frame(
            " paragon-stats 0.5.0   menu                                                                                 read-only *",
            @" C:\Games\Homecoming\accounts   2 live   unattributed 0",
            Rule,
            "  [1]  Live stats     per-character readout, updating while you play",
            "  [2]  Help           what every option does, and the exit codes",
            "  [q]  Quit",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            Rule,
            " [1] live   [2] help   [q] quit");

        Assert.Equal(expected, Render(new MenuScreen()), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void The_help_screen_states_the_surface_and_the_promises()
    {
        string frame = Render(new HelpScreen());

        // Every one of these must survive the strip's twelve rows. The first
        // draft ran eleven lines of help and lost the exit codes and the
        // read-only promise off the bottom without saying so.
        Assert.Contains("--watch", frame, StringComparison.Ordinal);
        Assert.Contains("--version", frame, StringComparison.Ordinal);
        Assert.Contains("0 success", frame, StringComparison.Ordinal);
        Assert.Contains("2 bad usage", frame, StringComparison.Ordinal);
        Assert.Contains("never writes to the game directory", frame, StringComparison.Ordinal);
        Assert.Contains("never collected", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_readout_explains_itself_rather_than_showing_a_blank_body()
    {
        Frame frame = new(120, 12);
        new LiveScreen().Render(frame, new Readout("0.5.0", @"C:\Games", Snapshot.Capture([], 0)));
        string text = frame.ToPlainText();

        // A blank body reads as broken, which is the failure this checkpoint exists to fix.
        Assert.Contains("Waiting for a character to log in", text, StringComparison.Ordinal);
        Assert.Contains("Log Chat", text, StringComparison.Ordinal);
        Assert.Contains("no live sessions", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_name_longer_than_its_column_is_clipped_not_wrapped()
    {
        SessionTracker tracker = new();
        tracker.Accept("acct", "2026-01-01 12:00:00 Welcome to City of Heroes, Extremely Long Character Name!");
        tracker.Accept("acct", "2026-01-01 12:05:00 You gain 10 experience.");

        Frame frame = new(120, 12);
        new LiveScreen().Render(frame, new Readout("0.5.0", "root", Snapshot.Capture(tracker)));
        string text = frame.ToPlainText();

        // 18 characters of CHARACTER, then the next column - never a wrap that
        // would shove every following column out of alignment.
        Assert.Contains("Extremely Long Cha acct", text, StringComparison.Ordinal);
        Assert.DoesNotContain("racter Name", text, StringComparison.Ordinal);
    }

    [Fact]
    public void More_boxes_than_rows_says_so_rather_than_dropping_them()
    {
        SessionTracker tracker = new();
        for (int box = 1; box <= 9; box++)
        {
            string account = string.Create(CultureInfo.InvariantCulture, $"acct{box}");
            tracker.Accept(
                account,
                string.Create(CultureInfo.InvariantCulture, $"2026-01-01 12:00:00 Welcome to City of Heroes, Box {box}!"));
            tracker.Accept(account, "2026-01-01 12:05:00 You gain 10 experience.");
        }

        Frame frame = new(120, 12);
        new LiveScreen().Render(frame, new Readout("0.5.0", "root", Snapshot.Capture(tracker)));
        string text = frame.ToPlainText();

        // Silently truncating would under-report the farm, which is the one
        // thing a farm readout must never do.
        Assert.Contains("more", text, StringComparison.Ordinal);
        Assert.Contains("Box 1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_single_box_gets_no_all_boxes_row()
    {
        SessionTracker tracker = new();
        tracker.Accept("acct", "2026-01-01 12:00:00 Welcome to City of Heroes, Solo!");
        tracker.Accept("acct", "2026-01-01 12:05:00 You gain 10 experience.");

        Frame frame = new(120, 12);
        new LiveScreen().Render(frame, new Readout("0.5.0", "root", Snapshot.Capture(tracker)));

        // A total identical to the only row above it is noise.
        Assert.DoesNotContain("ALL BOXES", frame.ToPlainText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData('q', ScreenResult.Quit)]
    [InlineData('Q', ScreenResult.Quit)]
    [InlineData('m', ScreenResult.Menu)]
    [InlineData('h', ScreenResult.Help)]
    [InlineData('z', ScreenResult.Stay)]
    [InlineData(' ', ScreenResult.Stay)]
    public void The_live_screen_routes_keys(char pressed, ScreenResult expected) =>
        Assert.Equal(expected, new LiveScreen().Key(pressed));

    [Theory]
    [InlineData('1', ScreenResult.Live)]
    [InlineData('2', ScreenResult.Help)]
    [InlineData('h', ScreenResult.Help)]
    [InlineData('q', ScreenResult.Quit)]
    [InlineData('x', ScreenResult.Stay)]
    public void The_menu_routes_keys(char pressed, ScreenResult expected) =>
        Assert.Equal(expected, new MenuScreen().Key(pressed));

    [Theory]
    [InlineData('1', ScreenResult.Live)]
    [InlineData('m', ScreenResult.Menu)]
    [InlineData('q', ScreenResult.Quit)]
    [InlineData('9', ScreenResult.Stay)]
    public void The_help_screen_routes_keys(char pressed, ScreenResult expected) =>
        Assert.Equal(expected, new HelpScreen().Key(pressed));

    [Fact]
    public void An_unknown_key_never_quits()
    {
        // A stray keystroke while playing must not end the session.
        IScreen[] screens = [new MenuScreen(), new LiveScreen(), new HelpScreen()];

        foreach (IScreen screen in screens)
        {
            foreach (char pressed in "abcdefgijklnoprstuvwxyz0345678")
            {
                Assert.NotEqual(ScreenResult.Quit, screen.Key(pressed));
            }
        }
    }

    [Fact]
    public void Every_screen_advertises_itself_in_the_footer()
    {
        // A screen cannot ship without appearing in the hints.
        foreach (IScreen screen in new IScreen[] { new MenuScreen(), new LiveScreen(), new HelpScreen() })
        {
            Assert.False(string.IsNullOrWhiteSpace(screen.Title));
            Assert.Contains("[q] quit", screen.Hints, StringComparison.Ordinal);
            Assert.Contains(screen.Hints, Render(screen), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Frame frame = new(10, 4);
        Readout readout = new("0.5.0", "root", Snapshot.Capture([], 0));

        Assert.Throws<ArgumentNullException>(() => new LiveScreen().Render(null!, readout));
        Assert.Throws<ArgumentNullException>(() => new LiveScreen().Render(frame, null!));
        Assert.Throws<ArgumentNullException>(() => new MenuScreen().Render(null!, readout));
        Assert.Throws<ArgumentNullException>(() => new MenuScreen().Render(frame, null!));
        Assert.Throws<ArgumentNullException>(() => new HelpScreen().Render(null!, readout));
        Assert.Throws<ArgumentNullException>(() => new HelpScreen().Render(frame, null!));
        Assert.Throws<ArgumentNullException>(() => new LiveScreen(null!));
    }

    private static string Render(IScreen screen)
    {
        Frame frame = new(120, 12);
        screen.Render(frame, new Readout("0.5.0", @"C:\Games\Homecoming\accounts", TwoBoxes()));
        return frame.ToPlainText();
    }

    /// <summary>
    /// Two boxes with continuous activity. The gaps matter: the idle timeout is
    /// 30 minutes, so a fixture with a two-hour hole in it closes its sessions
    /// and renders an empty readout.
    /// </summary>
    private static Snapshot TwoBoxes()
    {
        SessionTracker tracker = new();

        tracker.Accept("mrlaser", "2026-01-01 12:00:00 Welcome to City of Heroes, Laser - ALT F4!");
        tracker.Accept("mrlaser", "2026-01-01 12:00:05 You gain 1,200,000 experience and 4,000,000 influence.");
        tracker.Accept("mrlaser", "2026-01-01 12:00:10 You earned 312 architect tickets!");
        Sustain(tracker, "mrlaser", new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified), 120);

        tracker.Accept("mrlaser2", "2026-01-01 12:30:00 Welcome to City of Heroes, Fixture Scrapper!");
        tracker.Accept("mrlaser2", "2026-01-01 12:30:05 You gain 118,004 experience and 386,120 influence.");
        tracker.Accept("mrlaser2", "2026-01-01 12:30:10 You earned 50 architect tickets!");
        Sustain(tracker, "mrlaser2", new DateTime(2026, 1, 1, 12, 30, 0, DateTimeKind.Unspecified), 80);

        return Snapshot.Capture(tracker);
    }

    private static void Sustain(SessionTracker tracker, string account, DateTime start, int minutes)
    {
        for (int minute = 20; minute <= minutes; minute += 20)
        {
            string stamp = start.AddMinutes(minute).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            tracker.Accept(account, $"{stamp} You hit Gravedigger with your Blazing Aura for 16.98 points of Fire damage.");
        }
    }
}
