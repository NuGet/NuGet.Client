// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NuGet.Repo.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class HashSetStringComparerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NCA0003";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "HashSet with string element should specify a StringComparer";
        private static readonly LocalizableString MessageFormat = "HashSet creation with string element type should explicitly specify a StringComparer";
        private static readonly LocalizableString Description = "NuGet package ids and versions are case insensitive. File names and paths are OS dependent. Explicitly set the StringComparer to reduce risk of bugs.";

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

            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeImplicitObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(objectCreation, context.CancellationToken);

            if (symbolInfo.Symbol is IMethodSymbol constructor)
            {
                AnalyzeHashSetCreation(context, objectCreation, objectCreation.ArgumentList, constructor.ContainingType);
            }
        }

        private static void AnalyzeImplicitObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var implicitObjectCreation = (ImplicitObjectCreationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(implicitObjectCreation, context.CancellationToken);

            if (symbolInfo.Symbol is IMethodSymbol constructor)
            {
                AnalyzeHashSetCreation(context, implicitObjectCreation, implicitObjectCreation.ArgumentList, constructor.ContainingType);
            }
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);

            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                // Check for ImmutableHashSet.Create<T>(), ImmutableHashSet.CreateRange<T>(), or ImmutableHashSet.CreateBuilder<T>()
                if (methodSymbol.ContainingType?.ToDisplayString() == "System.Collections.Immutable.ImmutableHashSet" &&
                    (methodSymbol.Name == "Create" || methodSymbol.Name == "CreateRange" || methodSymbol.Name == "CreateBuilder") &&
                    methodSymbol.IsStatic &&
                    methodSymbol.TypeArguments.Length == 1)
                {
                    var elementType = methodSymbol.TypeArguments[0];

                    if (elementType.SpecialType == SpecialType.System_String)
                    {
                        if (invocation.ArgumentList != null && HasStringComparerArgument(context, invocation.ArgumentList))
                        {
                            return;
                        }

                        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                // Check for LINQ ToHashSet<TSource>() or ToImmutableHashSet<TSource>()
                else if (methodSymbol.IsExtensionMethod &&
                         (methodSymbol.Name == "ToHashSet" || methodSymbol.Name == "ToImmutableHashSet") &&
                         methodSymbol.TypeArguments.Length == 1)
                {
                    var elementType = methodSymbol.TypeArguments[0];

                    if (elementType.SpecialType == SpecialType.System_String)
                    {
                        if (invocation.ArgumentList != null && HasStringComparerArgument(context, invocation.ArgumentList))
                        {
                            return;
                        }

                        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static void AnalyzeHashSetCreation(SyntaxNodeAnalysisContext context, SyntaxNode node, ArgumentListSyntax? argumentList, ITypeSymbol typeSymbol)
        {
            if (!IsHashSetType(typeSymbol, out var elementType))
            {
                return;
            }

            if (elementType?.SpecialType != SpecialType.System_String)
            {
                return;
            }

            if (argumentList != null && HasStringComparerArgument(context, argumentList))
            {
                return;
            }

            if (!HasComparerConstructorOverload(typeSymbol, elementType))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, node.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsHashSetType(ITypeSymbol typeSymbol, out ITypeSymbol? elementType)
        {
            elementType = null;

            if (typeSymbol is not INamedTypeSymbol namedType)
                return false;

            if (IsHashSetInterface(namedType))
            {
                elementType = namedType.TypeArguments.FirstOrDefault();
                return true;
            }

            foreach (var @interface in namedType.AllInterfaces)
            {
                if (IsHashSetInterface(@interface))
                {
                    elementType = @interface.TypeArguments.FirstOrDefault();
                    return true;
                }
            }

            return false;
        }

        private static bool IsHashSetInterface(INamedTypeSymbol typeSymbol)
        {
            if (!typeSymbol.IsGenericType || typeSymbol.TypeArguments.Length != 1)
            {
                return false;
            }

            var originalDefinition = typeSymbol.OriginalDefinition;
            var fullName = originalDefinition.ToDisplayString();

            return fullName == "System.Collections.Generic.ISet<T>" ||
                   fullName == "System.Collections.Generic.HashSet<T>";
        }

        private static bool HasComparerConstructorOverload(ITypeSymbol typeSymbol, ITypeSymbol elementType)
        {
            if (typeSymbol is not INamedTypeSymbol namedType)
                return false;

            foreach (var constructor in namedType.Constructors)
            {
                foreach (var parameter in constructor.Parameters)
                {
                    if (parameter.Type is INamedTypeSymbol parameterType &&
                        parameterType.IsGenericType &&
                        parameterType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEqualityComparer<T>")
                    {
                        if (parameterType.TypeArguments.Length == 1 &&
                            SymbolEqualityComparer.Default.Equals(parameterType.TypeArguments[0], elementType))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool HasStringComparerArgument(SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList)
        {
            foreach (var argument in argumentList.Arguments)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken);
                if (typeInfo.Type != null)
                {
                    var typeName = typeInfo.Type.ToDisplayString();
                    if (typeName.Contains("StringComparer"))
                    {
                        return true;
                    }

                    if (IsIEqualityComparerOfString(typeInfo.Type))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsIEqualityComparerOfString(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                if (namedType.IsGenericType &&
                    namedType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEqualityComparer<T>" &&
                    namedType.TypeArguments.Length == 1 &&
                    namedType.TypeArguments[0].SpecialType == SpecialType.System_String)
                {
                    return true;
                }

                foreach (var @interface in namedType.AllInterfaces)
                {
                    if (@interface.IsGenericType &&
                        @interface.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEqualityComparer<T>" &&
                        @interface.TypeArguments.Length == 1 &&
                        @interface.TypeArguments[0].SpecialType == SpecialType.System_String)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
