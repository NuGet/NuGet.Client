// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NuGet.Repo.Analyzers
{
    /// <summary>
    /// Analyzer that detects calls to string.GetHashCode() and requires using StringComparer.GetHashCode() instead.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StringGetHashCodeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NCA0002";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "Use StringComparer.GetHashCode instead of string.GetHashCode";
        private static readonly LocalizableString MessageFormat = "Call to string.GetHashCode() should be replaced with StringComparer.GetHashCode(string)";
        private static readonly LocalizableString Description = "string.GetHashCode() is culture-sensitive and can produce different results on different platforms. Use StringComparer with an explicit comparison type to ensure consistent behavior.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Check if it's a member access expression (e.g., str.GetHashCode())
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                return;
            }

            // Check if the method name is GetHashCode
            if (memberAccess.Name.Identifier.Text != "GetHashCode")
            {
                return;
            }

            // Check if there are no arguments (GetHashCode should be parameterless)
            if (invocation.ArgumentList.Arguments.Count != 0)
            {
                return;
            }

            // Get the symbol information for the invocation
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                return;
            }

            // Check if the method is GetHashCode and it's defined on System.String
            if (methodSymbol.Name != "GetHashCode" ||
                methodSymbol.ContainingType?.SpecialType != SpecialType.System_String)
            {
                return;
            }

            // Verify it's the parameterless GetHashCode() method
            if (methodSymbol.Parameters.Length != 0)
            {
                return;
            }

            // Report diagnostic
            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
