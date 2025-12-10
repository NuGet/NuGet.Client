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
    /// <summary>
    /// Analyzer that ensures dictionaries with string keys explicitly specify a StringComparer.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DictionaryStringKeyComparerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NCA0001";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = "Dictionary with string key should specify a StringComparer";
        private static readonly LocalizableString MessageFormat = "Dictionary creation with string key type should explicitly specify a StringComparer";
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
                AnalyzeDictionaryCreation(context, objectCreation, objectCreation.ArgumentList, constructor.ContainingType, constructor);
            }
        }

        private static void AnalyzeImplicitObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var implicitObjectCreation = (ImplicitObjectCreationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(implicitObjectCreation, context.CancellationToken);

            if (symbolInfo.Symbol is IMethodSymbol constructor)
            {
                AnalyzeDictionaryCreation(context, implicitObjectCreation, implicitObjectCreation.ArgumentList, constructor.ContainingType, constructor);
            }
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);

            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                // Check for ImmutableDictionary.Create<TKey, TValue>(), ImmutableDictionary.CreateRange<TKey, TValue>(), or ImmutableDictionary.CreateBuilder<TKey, TValue>()
                if (methodSymbol.ContainingType?.ToDisplayString() == "System.Collections.Immutable.ImmutableDictionary" &&
                    (methodSymbol.Name == "Create" || methodSymbol.Name == "CreateRange" || methodSymbol.Name == "CreateBuilder") &&
                    methodSymbol.IsStatic &&
                    methodSymbol.TypeArguments.Length == 2)
                {
                    var keyType = methodSymbol.TypeArguments[0];

                    // Check if the key type is string
                    if (keyType.SpecialType == SpecialType.System_String)
                    {
                        // Check if a StringComparer argument is provided
                        if (invocation.ArgumentList != null && HasStringComparerArgument(context, invocation.ArgumentList))
                        {
                            return;
                        }

                        // Report diagnostic
                        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                // Check for LINQ ToDictionary<TSource, TKey, ...>() or ToImmutableDictionary<TSource, TKey, ...>()
                else if (methodSymbol.IsExtensionMethod &&
                         (methodSymbol.Name == "ToDictionary" || methodSymbol.Name == "ToImmutableDictionary") &&
                         methodSymbol.TypeArguments.Length >= 2)
                {
                    // For ToDictionary/ToImmutableDictionary, TypeArguments are: [TSource, TKey] or [TSource, TKey, TValue]
                    var keyType = methodSymbol.TypeArguments[1];

                    // Check if the key type is string
                    if (keyType.SpecialType == SpecialType.System_String)
                    {
                        // Check if a StringComparer argument is provided
                        if (invocation.ArgumentList != null && HasStringComparerArgument(context, invocation.ArgumentList))
                        {
                            return;
                        }

                        // Report diagnostic
                        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static void AnalyzeDictionaryCreation(SyntaxNodeAnalysisContext context, SyntaxNode node, ArgumentListSyntax? argumentList, ITypeSymbol typeSymbol, IMethodSymbol calledConstructor)
        {
            // Check if it's a dictionary type
            if (!IsDictionaryType(typeSymbol, out var keyType))
            {
                return;
            }

            // Check if the key type is string
            if (keyType?.SpecialType != SpecialType.System_String)
            {
                return;
            }

            // Check if a StringComparer argument is provided
            if (argumentList != null && HasStringComparerArgument(context, argumentList))
            {
                return;
            }

            // Check if the type has a constructor overload that accepts an IEqualityComparer<TKey>
            // If no such overload exists, the type cannot accept a comparer, so don't report a diagnostic
            if (!HasComparerConstructorOverload(typeSymbol, keyType))
            {
                return;
            }

            // Report diagnostic
            var diagnostic = Diagnostic.Create(Rule, node.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsDictionaryType(ITypeSymbol typeSymbol, out ITypeSymbol? keyType)
        {
            keyType = null;

            if (typeSymbol is not INamedTypeSymbol namedType)
                return false;

            // Check for Dictionary<TKey, TValue>
            if (IsDictionaryInterface(namedType))
            {
                keyType = namedType.TypeArguments.FirstOrDefault();
                return true;
            }

            // Check for types implementing IDictionary<TKey, TValue>
            foreach (var @interface in namedType.AllInterfaces)
            {
                if (IsDictionaryInterface(@interface))
                {
                    keyType = @interface.TypeArguments.FirstOrDefault();
                    return true;
                }
            }

            return false;
        }

        private static bool IsDictionaryInterface(INamedTypeSymbol typeSymbol)
        {
            if (!typeSymbol.IsGenericType || typeSymbol.TypeArguments.Length != 2)
            {
                return false;
            }

            var originalDefinition = typeSymbol.OriginalDefinition;
            var fullName = originalDefinition.ToDisplayString();

            return fullName == "System.Collections.Generic.IDictionary<TKey, TValue>" ||
                   fullName == "System.Collections.Generic.Dictionary<TKey, TValue>" ||
                   fullName == "System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>";
        }

        private static bool HasComparerConstructorOverload(ITypeSymbol typeSymbol, ITypeSymbol keyType)
        {
            if (typeSymbol is not INamedTypeSymbol namedType)
                return false;

            // Check if any constructor has a parameter that is IEqualityComparer<TKey> or IComparer<TKey>
            foreach (var constructor in namedType.Constructors)
            {
                foreach (var parameter in constructor.Parameters)
                {
                    if (parameter.Type is INamedTypeSymbol parameterType &&
                        parameterType.IsGenericType)
                    {
                        var parameterTypeName = parameterType.OriginalDefinition.ToDisplayString();
                        if (parameterTypeName == "System.Collections.Generic.IEqualityComparer<T>" ||
                            parameterTypeName == "System.Collections.Generic.IComparer<T>")
                        {
                            // Check if the type argument matches the key type
                            if (parameterType.TypeArguments.Length == 1 &&
                                SymbolEqualityComparer.Default.Equals(parameterType.TypeArguments[0], keyType))
                            {
                                return true;
                            }
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

                    if (IsIEqualityComparerOfString(typeInfo.Type) || IsIComparerOfString(typeInfo.Type))
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

        private static bool IsIComparerOfString(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                if (namedType.IsGenericType &&
                    namedType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IComparer<T>" &&
                    namedType.TypeArguments.Length == 1 &&
                    namedType.TypeArguments[0].SpecialType == SpecialType.System_String)
                {
                    return true;
                }

                foreach (var @interface in namedType.AllInterfaces)
                {
                    if (@interface.IsGenericType &&
                        @interface.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IComparer<T>" &&
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
