// check-mutations.cs - prove the tests can actually fail.
//
//   dotnet run scripts/dev/check-mutations.cs
//
// Why this exists: a passing test proves nothing on its own. Break the code it
// covers; if the test still passes, it was never testing anything. This project
// has shipped that mistake three times in a row on ONE assertion, each version
// written as the fix for the last:
//
//   Assert.NotNull(env.ClientRunning)      - non-nullable property with a
//     non-null default; passed with the production wiring deleted outright.
//   Assert.False(env.ClientCount() > 0 && !env.ClientRunning())
//     - `&&` short-circuits, so ClientRunning was never invoked at all.
//   the same with locals - invokes both, but on a machine with no game the
//     count is zero and the conjunction is false whatever the other returns.
//
// None was caught by the 100% coverage gate, CodeQL, Sonar or code review. All
// three were caught the moment somebody broke the code on purpose.
//
// This is the interim. Stryker.NET generates mutants mechanically and finds the
// ones nobody thought to try; this file only re-checks the handful we know
// matter, so the knowledge lives in the repo instead of in a session that ends.
// See #269 - when Stryker lands, port these and delete this script.
//
// NOT wired into the hooks: it rebuilds and re-runs a filtered suite per
// mutation, which is too slow for pre-commit and too slow for pre-push. Run it
// when touching CliEnvironment, TuiHost, or any test that asserts on them.
//
// A BUILD FAILURE IS NOT A KILL. An exit code cannot tell "the test failed"
// from "the code no longer compiles", and that trap has already been hit here:
// a mutation was recorded as caught when it had merely broken the build. The
// two are reported separately below, deliberately.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Diagnostics;

string core = Path.Combine("src", "ParagonStats.Core");
string cliEnvironment = Path.Combine(core, "Stats", "CliEnvironment.cs");
string tuiHost = Path.Combine(core, "Tui", "TuiHost.cs");

// Each entry: what the mutation breaks, the file, the exact text to replace,
// what to replace it with, and the test that must notice.
(string Label, string File, string Original, string Mutated, string Test)[] mutations =
[
    (
        "a key that means nothing quits instead of staying",
        tuiHost,
        "if (result == ScreenResult.Quit)",
        "if (result is ScreenResult.Quit or ScreenResult.Stay)",
        "A_key_that_means_nothing_leaves_you_on_the_screen_you_were_on"),
    (
        "the default key reader claims a keypress",
        cliEnvironment,
        "public Func<char?> ReadKey { get; init; } = static () => null;",
        "public Func<char?> ReadKey { get; init; } = static () => 'x';",
        "The_default_key_reader_reports_no_keypress_rather_than_blocking"),
    (
        "a forced run reports its client absent",
        cliEnvironment,
        "ClientRunning = forced\n                ? static () => true",
        "ClientRunning = forced\n                ? static () => false",
        "The_production_wiring_is_invoked_not_merely_assigned"),
    (
        "the forced window loses the shell's cursor row",
        cliEnvironment,
        "WindowSize = forced ? static () => (120, 13)",
        "WindowSize = forced ? static () => (120, 12)",
        "The_production_wiring_is_invoked_not_merely_assigned"),
    (
        "end of input stops meaning quit",
        cliEnvironment,
        "return next < 0 ? 'q' : (char)next;",
        "return next < 0 ? 'x' : (char)next;",
        "A_forced_run_reads_keys_from_the_pipe_and_treats_end_of_input_as_quit"),
    (
        "the forced client count stops being pinned",
        cliEnvironment,
        "ClientCount = forced ? static () => 0",
        "ClientCount = forced ? static () => 7",
        "A_forced_run_pins_the_client_count_so_a_golden_never_depends_on_what_else_is_open"),
];

int survived = 0;
int unusable = 0;

foreach ((string label, string file, string original, string mutated, string test) in mutations)
{
    if (!File.Exists(file))
    {
        Console.WriteLine($"[??] {label}: {file} not found");
        unusable++;
        continue;
    }

    string source = File.ReadAllText(file);
    if (!source.Contains(original, StringComparison.Ordinal))
    {
        // The code moved on and the mutation no longer applies. That is a
        // finding, not a pass: the mutation set has to be maintained with the
        // code it guards, or it quietly stops guarding anything.
        Console.WriteLine($"[??] {label}: anchor text no longer present in {file}");
        unusable++;
        continue;
    }

    File.WriteAllText(file, source.Replace(original, mutated, StringComparison.Ordinal));
    try
    {
        (int code, string output) = Run("dotnet", $"test --filter {test}");
        bool broke = output.Contains("error CS", StringComparison.Ordinal)
            || output.Contains("Build failed", StringComparison.Ordinal);

        if (broke)
        {
            Console.WriteLine($"[!!] {label}: BUILD FAILED - this is not a kill, the mutation is unusable");
            unusable++;
        }
        else if (code != 0)
        {
            Console.WriteLine($"[OK] {label}: caught by {test}");
        }
        else
        {
            Console.WriteLine($"[XX] {label}: SURVIVED - {test} passed with the code broken");
            survived++;
        }
    }
    finally
    {
        File.WriteAllText(file, source);
    }
}

Console.WriteLine();
if (survived == 0 && unusable == 0)
{
    Console.WriteLine($"[OK] all {mutations.Length} mutations caught.");
    return 0;
}

Console.WriteLine($"{survived} survived, {unusable} unusable, of {mutations.Length}.");
return 1;

static (int Code, string Output) Run(string file, string arguments)
{
    ProcessStartInfo info = new(file, arguments)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    using Process process = Process.Start(info)!;
    string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    process.WaitForExit();
    return (process.ExitCode, output);
}
