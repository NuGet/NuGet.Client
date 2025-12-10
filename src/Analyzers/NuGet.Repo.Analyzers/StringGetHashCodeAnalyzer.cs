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
        private static readonly LocalizableString Description = "Make sure to use the correct comparer for package ids, versions, and so on.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/NuGet/NuGet.Client/tree/dev/src/Analyzers/NuGet.Repo.Analyzers/docs/NCA0002.md");

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

            // Support both direct member access (str.GetHashCode()) and conditional member access (str?.GetHashCode())
            MemberAccessExpressionSyntax? memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            MemberBindingExpressionSyntax? memberBinding = invocation.Expression as MemberBindingExpressionSyntax;
            if (memberAccess is null && memberBinding is null)
            {
                return;
            }

            // Check if the method name is GetHashCode
            var nameIdentifier = memberAccess?.Name.Identifier ?? memberBinding!.Name.Identifier;
            if (nameIdentifier.ValueText != "GetHashCode")
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

            // Check method is parameterless GetHashCode()
            if (methodSymbol.Name != "GetHashCode")
            {
                return;
            }

            if (methodSymbol.Parameters.Length != 0)
            {
                return;
            }

            // Determine the receiver type. For conditional access, get the conditional receiver expression.
            ITypeSymbol? receiverType = null;
            if (memberAccess is not null)
            {
                var receiver = memberAccess.Expression;
                var receiverTypeInfo = context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken);
                receiverType = receiverTypeInfo.Type;
            }
            else if (memberBinding is not null)
            {
                var conditional = invocation.Parent as ConditionalAccessExpressionSyntax;
                if (conditional is null)
                {
                    // If not part of a conditional access, bail.
                    return;
                }
                var receiverTypeInfo = context.SemanticModel.GetTypeInfo(conditional.Expression, context.CancellationToken);
                receiverType = receiverTypeInfo.Type;
            }

            // Only report when the receiver is a string (even if the method resolves to object.GetHashCode due to boxing/casting)
            bool isStringReceiver = receiverType?.SpecialType == SpecialType.System_String;

            // Also allow direct resolution to string.GetHashCode as an alternative signal
            bool isStringMethod = methodSymbol.ContainingType?.SpecialType == SpecialType.System_String;

            if (!isStringReceiver && !isStringMethod)
            {
                return;
            }

            // Report diagnostic
            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
