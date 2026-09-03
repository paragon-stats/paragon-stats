namespace ParagonStats.Core.Tui;

/// <summary>
/// The landing screen. It exists because double-clicking the binary used to
/// open a window with nothing to type into; every destination the text UI has
/// is reachable from here, and the frame says so rather than expecting anyone
/// to guess a flag.
/// </summary>
public sealed class MenuScreen : IScreen
{
    public string Title => "menu";

    public string Hints => "[1] live   [2] help   [q] quit";

    public void Render(Frame frame, Readout readout)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(readout);

        Chrome.Header(frame, readout, Title);

        frame.Write(Chrome.BodyTop, 2, "[1]  Live stats     per-character readout, updating while you play");
        frame.Write(Chrome.BodyTop + 1, 2, "[2]  Help           what every option does, and the exit codes");
        frame.Write(Chrome.BodyTop + 2, 2, "[q]  Quit");

        Chrome.Footer(frame, Hints);
    }

    public ScreenResult Key(char pressed) => pressed switch
    {
        '1' => ScreenResult.Live,
        '2' or 'h' or 'H' => ScreenResult.Help,
        'q' or 'Q' => ScreenResult.Quit,
        _ => ScreenResult.Stay,
    };
}
