namespace ParagonStats.Core.Tui;

/// <summary>What a keypress asked for. The host owns the switch; a screen only reports.</summary>
public enum ScreenResult
{
    /// <summary>Nothing happened, or the key meant nothing here. Keep painting this screen.</summary>
    Stay,

    /// <summary>Leave the text UI.</summary>
    Quit,

    /// <summary>Show the menu.</summary>
    Menu,

    /// <summary>Show the live readout.</summary>
    Live,

    /// <summary>Show the help screen.</summary>
    Help,
}
