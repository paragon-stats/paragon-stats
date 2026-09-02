using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ParagonStats.Analyzers;

/// <summary>
/// PS0001 - an identifier shorter than three characters says nothing about
/// what it holds. The homelab repos enforce the same rule for Python
/// (scripts/linting/check_short_identifier_names.py); nothing in the C#
/// analyzer stack can express a minimum length - StyleCop's SA13xx family,
/// Meziantou's rules and dotnet_naming_style are all casing, prefix and
/// suffix only - so this analyzer is the C# half of that convention.
/// Scope matches the Python checker's: things a reader meets inside a method
/// body or signature. Type and member names are out of scope.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ShortIdentifierAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PS0001";

    /// <summary>Measured after leading underscores are stripped, so `_id` violates and `_` does not.</summary>
    private const int MinimumLength = 3;

    /// <summary>
    /// The exemptions, and only these: the discard, the two domain
    /// abbreviations the game itself uses (and which already appear as
    /// xpRate/infRate), and the classic loop counters. `ex` is deliberately
    /// absent - a caught exception is named `exception`.
    /// </summary>
    private static readonly ImmutableHashSet<string> Allowed =
        ImmutableHashSet.Create(StringComparer.Ordinal, "_", "xp", "inf", "i", "j", "k");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Identifier is too short",
        "'{0}' is shorter than {1} characters - name it for what it holds",
        "Naming",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Short identifiers force the reader to reconstruct meaning from context. Allowed: _, xp, inf, i, j, k.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeVariable, SyntaxKind.VariableDeclarator);
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
        context.RegisterSyntaxNodeAction(AnalyzeForEach, SyntaxKind.ForEachStatement);
        context.RegisterSyntaxNodeAction(AnalyzeCatch, SyntaxKind.CatchDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeDesignation, SyntaxKind.SingleVariableDesignation);
    }

    private static void AnalyzeVariable(SyntaxNodeAnalysisContext context) =>
        Check(context, ((VariableDeclaratorSyntax)context.Node).Identifier);

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context) =>
        Check(context, ((ParameterSyntax)context.Node).Identifier);

    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context) =>
        Check(context, ((ForEachStatementSyntax)context.Node).Identifier);

    private static void AnalyzeCatch(SyntaxNodeAnalysisContext context) =>
        Check(context, ((CatchDeclarationSyntax)context.Node).Identifier);

    private static void AnalyzeDesignation(SyntaxNodeAnalysisContext context) =>
        Check(context, ((SingleVariableDesignationSyntax)context.Node).Identifier);

    private static void Check(SyntaxNodeAnalysisContext context, SyntaxToken identifier)
    {
        string name = identifier.ValueText;
        if (name.Length == 0 || Allowed.Contains(name))
        {
            return;
        }

        // Leading underscores are decoration, not meaning: a private field
        // named _id is as opaque as one named id.
        string bare = name.TrimStart('_');
        if (bare.Length == 0 || bare.Length >= MinimumLength)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), name, MinimumLength));
    }
}
