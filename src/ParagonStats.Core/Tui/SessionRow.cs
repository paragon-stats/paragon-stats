namespace ParagonStats.Core.Tui;

/// <summary>
/// One character's numbers, copied out of the live model at the instant a frame
/// is built. Values rather than a reference to <c>SessionStats</c> on purpose:
/// the tracker mutates on the next tick, and a row that changes while a screen
/// is drawing it is a bug waiting for the GUI in CP4 to find. Every scalar the
/// engine folds is here, so a new metric column needs a descriptor rather than
/// a change to this shape.
/// </summary>
/// <param name="Character">The logged-in character this row belongs to.</param>
/// <param name="Account">The account folder the log came from.</param>
/// <param name="Clock">Wall-clock span from session start to last seen, never negative.</param>
/// <param name="Experience">Experience earned.</param>
/// <param name="Influence">Influence or infamy earned.</param>
/// <param name="Tickets">Architect tickets earned.</param>
/// <param name="Defeats">Foes defeated by this character.</param>
/// <param name="Activations">Powers activated.</param>
/// <param name="Damage">Total damage dealt.</param>
/// <param name="MarketIncome">Influence received from the market.</param>
/// <param name="MarketSpent">Influence paid to the market.</param>
public sealed record SessionRow(
    string Character,
    string Account,
    TimeSpan Clock,
    long Experience,
    long Influence,
    long Tickets,
    long Defeats,
    long Activations,
    decimal Damage,
    long MarketIncome,
    long MarketSpent);
