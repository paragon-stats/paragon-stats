using System.Diagnostics;

using ParagonStats.Core.Stats;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // let the watch loop exit and print its final summary
    cancellation.Cancel();
};

return CliRunner.Run(args, Console.Out, Console.Error, new CliEnvironment
{
    Input = Console.In,
    ClientRunning = static () =>
    {
        Process[] processes = Process.GetProcessesByName("cityofheroes");
        foreach (Process process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    },
    Token = cancellation.Token,
});
