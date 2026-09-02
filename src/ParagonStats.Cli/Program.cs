using ParagonStats.Core.Stats;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, eventArgs) =>
{
    // First Ctrl+C: graceful (watch prints its final summary). Second: kill.
    eventArgs.Cancel = !cancellation.IsCancellationRequested;
    cancellation.Cancel();
};

return CliRunner.Run(args, Console.Out, Console.Error, CliEnvironment.Production(cancellation.Token));
