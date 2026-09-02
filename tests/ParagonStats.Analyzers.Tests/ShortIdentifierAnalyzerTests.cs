using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ParagonStats.Analyzers.Tests;

/// <summary>
/// PS0001 drives a real compilation rather than a string match, so what is
/// asserted is what the compiler will report at build time.
/// </summary>
public sealed class ShortIdentifierAnalyzerTests
{
    [Theory]

    // Locals, in every shape a method body produces them.
    [InlineData("class C { void M() { int ab = 1; } }", "ab")]
    [InlineData("class C { void M() { for (int qq = 0; qq < 1; qq++) { } } }", "qq")]
    [InlineData("class C { void M(System.Collections.Generic.List<int> items) { foreach (var it in items) { } } }", "it")]
    [InlineData("class C { void M() { try { } catch (System.Exception e) { } } }", "e")]
    [InlineData("class C { bool M(string text) => int.TryParse(text, out var vv); }", "vv")]
    [InlineData("class C { void M(object value) { if (value is string st) { } } }", "st")]

    // Parameters, including lambdas.
    [InlineData("class C { void M(int ab) { } }", "ab")]
    [InlineData("using System; class C { void M() { Func<int, int> mapper = a => a; } }", "a")]

    // Leading underscores are decoration, not meaning.
    [InlineData("class C { private int _id; }", "_id")]
    public void Short_identifiers_are_reported(string source, string expected)
    {
        Diagnostic diagnostic = Assert.Single(Analyze(source));
        Assert.Equal(ShortIdentifierAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains(expected, diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Theory]

    // The allowlist, and only the allowlist.
    [InlineData("class C { void M() { for (int i = 0; i < 1; i++) { } } }")]
    [InlineData("class C { void M() { for (int j = 0; j < 1; j++) { } } }")]
    [InlineData("class C { void M() { for (int k = 0; k < 1; k++) { } } }")]
    [InlineData("class C { void M() { long xp = 1; long inf = 2; } }")]
    [InlineData("class C { void M() { var (_, second) = (1, 2); } }")]
    [InlineData("class C { private int _; }")]

    // Names long enough to say something.
    [InlineData("class C { void M(int count) { } }")]
    [InlineData("class C { void M() { try { } catch (System.Exception exception) { } } }")]

    // Out of scope: types, members, type parameters.
    [InlineData("class Ab { }")]
    [InlineData("class C { int Ab => 1; }")]
    [InlineData("class C<T> { }")]
    public void Acceptable_identifiers_are_left_alone(string source) => Assert.Empty(Analyze(source));

    [Fact]
    public void Every_short_identifier_in_a_file_is_reported()
    {
        const string source = "class C { void M(int ab) { int cd = ab; System.Func<int, int> mapper = e => e; } }";

        ImmutableArray<Diagnostic> diagnostics = Analyze(source);

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity));
    }

    [Fact]
    public void Initialize_tolerates_a_null_context()
    {
        // CA1062 wants the guard; Roslyn never exercises it, so this does.
        ShortIdentifierAnalyzer analyzer = new();

        Assert.Null(Record.Exception(() => analyzer.Initialize(null!)));
        Assert.Equal(ShortIdentifierAnalyzer.DiagnosticId, Assert.Single(analyzer.SupportedDiagnostics).Id);
    }

    private static ImmutableArray<Diagnostic> Analyze(string source)
    {
        string platform = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        IEnumerable<MetadataReference> references = platform
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "PS0001Tests",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation
            .WithAnalyzers([new ShortIdentifierAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken)
            .GetAwaiter()
            .GetResult();
    }
}
