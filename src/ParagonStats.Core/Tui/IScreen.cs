namespace ParagonStats.Core.Tui;

/// <summary>
/// One screen of the text UI. Three exist at birth - menu, live and help - so
/// the abstraction is earned rather than speculative, and a fourth is a class
/// plus one registration.
/// <para>
/// Screens render; they do not compute. Anything a screen would work out for
/// itself belongs in <see cref="Snapshot"/> or a <see cref="Column"/>, so the
/// GUI in CP4 can bind the same values instead of reimplementing them.
/// </para>
/// </summary>
public interface IScreen
{
    /// <summary>Shown in the chrome so it is always clear which screen is up.</summary>
    string Title { get; }

    /// <summary>The key hints this screen offers, rendered into the footer.</summary>
    string Hints { get; }

    /// <summary>Paints the whole frame. The frame arrives blank.</summary>
    void Render(Frame frame, Readout readout);

    /// <summary>
    /// Reports what a keypress asked for. Returning <see cref="ScreenResult.Stay"/>
    /// for an unknown key is deliberate: a stray keystroke during play should
    /// do nothing, never quit.
    /// </summary>
    ScreenResult Key(char pressed);
}
