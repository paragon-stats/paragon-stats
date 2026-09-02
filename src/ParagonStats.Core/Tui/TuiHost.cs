using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tui;

/// <summary>
/// Owns the loop: size the frame to the window, take a snapshot, paint, read a
/// key. The only piece that knows about time and the console, so screens stay
/// pure functions of a <see cref="Readout"/>.
/// </summary>
public static class TuiHost
{
    /// <summary>How long between repaints. Short enough that the clock looks live, long enough to cost nothing.</summary>
    private const int TickMilliseconds = 500;

    /// <summary>
    /// Runs until the user quits or the token cancels. `advance` pumps the
    /// engine and hands back the state for this frame; the host never touches
    /// the tracker itself.
    /// </summary>
    public static int Run(TextWriter output, Func<Snapshot> advance, string version, string root, CliEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(advance);
        ArgumentNullException.ThrowIfNull(env);

        IScreen current = new MenuScreen();
        while (!env.Token.IsCancellationRequested)
        {
            Readout readout = new(version, root, advance());
            output.Write(Paint(current, readout, env));

            char? pressed = env.ReadKey();
            if (pressed is null)
            {
                env.Sleep(TickMilliseconds);
                continue;
            }

            ScreenResult result = current.Key(pressed.Value);
            if (result == ScreenResult.Quit)
            {
                break;
            }

            // No sleep after a key: a burst of input drains at once, repainting
            // between each one. An earlier version read every waiting key in a
            // single tick and kept only the last, so typing "1" then "q"
            // quickly meant the live readout was never drawn at all.
            current = Switch(current, result);
        }

        return 0;
    }

    /// <summary>Renders one frame, sized to the window as it is right now.</summary>
    internal static string Paint(IScreen screen, Readout readout, CliEnvironment env)
    {
        (int width, int height) = env.WindowSize();

        // The last row belongs to the shell's cursor: painting into it scrolls
        // the frame up by one and every repaint walks down the screen.
        Frame frame = new(Math.Max(width, 20), Math.Max(height - 1, 6));
        screen.Render(frame, readout);
        return env.Interactive ? frame.ToAnsi() : frame.ToPlainText() + Environment.NewLine;
    }

    private static IScreen Switch(IScreen current, ScreenResult result) => result switch
    {
        ScreenResult.Menu => new MenuScreen(),
        ScreenResult.Live => new LiveScreen(),
        ScreenResult.Help => new HelpScreen(),
        _ => current,
    };
}
