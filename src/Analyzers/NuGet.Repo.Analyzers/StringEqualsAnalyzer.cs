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
    /// Analyzer that detects string equality comparisons using string.Equals or == operator
    /// and requires using IEqualityComparer.Equals method instead.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StringEqualsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NCA0004";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "Use IEqualityComparer.Equals instead of string equality comparison";
        private static readonly LocalizableString MessageFormat = "String equality comparison should use IEqualityComparer.Equals method with explicit StringComparer";
        private static readonly LocalizableString Description = "NuGet package ids and versions are case insensitive. File names and paths are OS dependent. MSBuild property names are case insensitive. Use StringComparer with an explicit comparison type to ensure consistent behavior.";

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
            context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.EqualsExpression);
            context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.NotEqualsExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Get the symbol information for the invocation
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                return;
            }

            // Check if the method is Equals
            if (methodSymbol.Name != "Equals")
            {
                return;
            }

            // Check for string.Equals(string) instance method
            if (methodSymbol.ContainingType?.SpecialType == SpecialType.System_String &&
                !methodSymbol.IsStatic &&
                methodSymbol.Parameters.Length == 1 &&
                methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String)
            {
                // Report diagnostic
                var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                context.ReportDiagnostic(diagnostic);
                return;
            }

            // Check for string.Equals(string, string) static method without StringComparison
            if (methodSymbol.ContainingType?.SpecialType == SpecialType.System_String &&
                methodSymbol.IsStatic &&
                methodSymbol.Parameters.Length == 2 &&
                methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_String)
            {
                // Report diagnostic
                var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                context.ReportDiagnostic(diagnostic);
                return;
            }

            // Check for object.Equals(object) when comparing strings
            if (methodSymbol.ContainingType?.SpecialType == SpecialType.System_Object &&
                !methodSymbol.IsStatic &&
                methodSymbol.Parameters.Length == 1 &&
                methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_Object)
            {
                // Check if the receiver is a string
                var receiverType = GetReceiverType(context, invocation);
                if (receiverType?.SpecialType == SpecialType.System_String)
                {
                    // Report diagnostic
                    var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }

            // Check for object.Equals(object, object) static method when comparing strings
            if (methodSymbol.ContainingType?.SpecialType == SpecialType.System_Object &&
                methodSymbol.IsStatic &&
                methodSymbol.Parameters.Length == 2 &&
                methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_Object)
            {
                // Check if any argument is a string
                if (invocation.ArgumentList?.Arguments.Count == 2)
                {
                    var arg1Type = context.SemanticModel.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression, context.CancellationToken).Type;
                    var arg2Type = context.SemanticModel.GetTypeInfo(invocation.ArgumentList.Arguments[1].Expression, context.CancellationToken).Type;

                    if (arg1Type?.SpecialType == SpecialType.System_String || arg2Type?.SpecialType == SpecialType.System_String)
                    {
                        // Report diagnostic
                        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                        return;
                    }
                }
            }
        }

        private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
        {
            var binaryExpression = (BinaryExpressionSyntax)context.Node;

            // Ignore comparisons against null (allowed for null-checks)
            if (binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression) ||
                binaryExpression.Right.IsKind(SyntaxKind.NullLiteralExpression))
            {
                return;
            }

            // Ignore comparisons against string.Empty (allowed for empty checks)
            if (IsStringEmpty(context, binaryExpression.Left) || IsStringEmpty(context, binaryExpression.Right))
            {
                return;
            }

            // Get type information for both sides of the comparison
            var leftType = context.SemanticModel.GetTypeInfo(binaryExpression.Left, context.CancellationToken).Type;
            var rightType = context.SemanticModel.GetTypeInfo(binaryExpression.Right, context.CancellationToken).Type;

            // Check if either side is a string
            if (leftType?.SpecialType == SpecialType.System_String || rightType?.SpecialType == SpecialType.System_String)
            {
                // Report diagnostic
                var diagnostic = Diagnostic.Create(Rule, binaryExpression.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsStringEmpty(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
                if (symbolInfo is IFieldSymbol fieldSymbol &&
                    fieldSymbol.ContainingType?.SpecialType == SpecialType.System_String &&
                    fieldSymbol.Name == "Empty")
                {
                    return true;
                }
            }

            return false;
        }

        private static ITypeSymbol? GetReceiverType(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
            }

            return null;
        }
    }
}
