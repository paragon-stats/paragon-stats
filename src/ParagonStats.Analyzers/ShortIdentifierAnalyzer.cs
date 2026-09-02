using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ParagonStats.Analyzers;

/// <summary>
/// PS0001 - an identifier shorter than three characters says nothing about
/// what it holds. The rule, the allowlist and why nothing off the shelf can
/// express it are documented once in docs/style-guides/csharp.md (#234).
/// Scope: locals, parameters, foreach/for variables, catch declarations,
/// pattern designations, and fields (a variable declarator covers fields,
/// consts and event fields). Types, methods and properties are not.
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
        // The house guard is throw-on-null (ArgumentNullException.ThrowIfNull
        // everywhere in Core); netstandard2.0 lacks the helper, not the rule.
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
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
        // Leading underscores are decoration, not meaning: a private field
        // named _id is as opaque as one named id. Strip them before both the
        // length test and the allowlist, or `_xp` would fail a rule that
        // clears `xp` - and `_camelCase` is this repo's field convention.
        string bare = identifier.ValueText.TrimStart('_');
        if (bare.Length == 0 || bare.Length >= MinimumLength || Allowed.Contains(bare))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), identifier.ValueText, MinimumLength));
    }
}
