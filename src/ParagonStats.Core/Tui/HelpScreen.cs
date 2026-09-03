namespace ParagonStats.Core.Tui;

/// <summary>
/// Help inside the frame rather than as a mode you exit into, so `--help` has a
/// visible home on screen like every other flag.
/// </summary>
public sealed class HelpScreen : IScreen
{
    /// <summary>
    /// Seven lines, because the strip is twelve rows and the chrome takes five.
    /// Anything longer is silently clipped, and losing the exit codes or the
    /// read-only promise off the bottom is worse than saying less.
    /// </summary>
    private static readonly string[] Lines =
    [
        "Reads your Homecoming chat logs and reports what you earned.",
        string.Empty,
        "  paragon-stats        menu, then live stats       --watch     straight to live",
        "  paragon-stats [path] a chatlog file or game dir  --version   print and exit",
        string.Empty,
        "exit codes:  0 success    1 no chatlogs or no game location    2 bad usage",
        "read-only: never writes to the game directory; chat channels are never collected",
    ];

    public string Title => "help";

    public string Hints => "[m] menu   [1] live   [q] quit";

    public void Render(Frame frame, Readout readout)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(readout);

        Chrome.Header(frame, readout, Title);

        int lastBodyRow = frame.Height - Chrome.FooterRows - 1;
        for (int index = 0; index < Lines.Length && Chrome.BodyTop + index <= lastBodyRow; index++)
        {
            frame.Write(Chrome.BodyTop + index, 1, Lines[index]);
        }

        Chrome.Footer(frame, Hints);
    }

    public ScreenResult Key(char pressed) => pressed switch
    {
        '1' => ScreenResult.Live,
        'm' or 'M' => ScreenResult.Menu,
        'q' or 'Q' => ScreenResult.Quit,
        _ => ScreenResult.Stay,
    };
}
