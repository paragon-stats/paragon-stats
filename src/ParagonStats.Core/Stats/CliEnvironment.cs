namespace ParagonStats.Core.Stats;

/// <summary>
/// The CLI's touchpoints with the outside world, injectable so every flow is
/// testable and coverage-measured: interactive input, the config location,
/// the game-client-process check, and watch-loop pacing.
/// </summary>
public sealed class CliEnvironment
{
    public TextReader? Input { get; init; }

    public string ConfigPath { get; init; } =
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "paragon-stats", "config.json");

    public Func<bool> ClientRunning { get; init; } = static () => true;

    public Action<int> Sleep { get; init; } = Thread.Sleep;

    public CancellationToken Token { get; init; }
}
