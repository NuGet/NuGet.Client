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
        private static readonly LocalizableString Description = "Make sure to use the correct comparer for package ids, versions, and so on.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/NuGet/NuGet.Client/tree/dev/src/Analyzers/NuGet.Repo.Analyzers/docs/NCA0001.md");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startContext =>
            {
                var compilation = startContext.Compilation;

                var cache = new SymbolCache
                {
                    IDictionary2 = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2"),
                    Dictionary2 = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2"),
                    ConcurrentDictionary2 = compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentDictionary`2"),
                    IEqualityComparer1 = compilation.GetTypeByMetadataName("System.Collections.Generic.IEqualityComparer`1"),
                    IComparer1 = compilation.GetTypeByMetadataName("System.Collections.Generic.IComparer`1"),
                    StringComparer = compilation.GetTypeByMetadataName("System.StringComparer"),
                    ImmutableDictionary = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary")
                };

                startContext.RegisterSyntaxNodeAction(ctx => AnalyzeObjectCreation(ctx, cache), SyntaxKind.ObjectCreationExpression);
                startContext.RegisterSyntaxNodeAction(ctx => AnalyzeImplicitObjectCreation(ctx, cache), SyntaxKind.ImplicitObjectCreationExpression);
                startContext.RegisterSyntaxNodeAction(ctx => AnalyzeInvocation(ctx, cache), SyntaxKind.InvocationExpression);
            });
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, SymbolCache cache)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(objectCreation, context.CancellationToken);

            if (symbolInfo.Symbol is IMethodSymbol constructor)
            {
                AnalyzeDictionaryCreation(context, cache, objectCreation, objectCreation.ArgumentList, constructor.ContainingType, constructor);
            }
        }

        private static void AnalyzeImplicitObjectCreation(SyntaxNodeAnalysisContext context, SymbolCache cache)
        {
            var implicitObjectCreation = (ImplicitObjectCreationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(implicitObjectCreation, context.CancellationToken);

            if (symbolInfo.Symbol is IMethodSymbol constructor)
            {
                AnalyzeDictionaryCreation(context, cache, implicitObjectCreation, implicitObjectCreation.ArgumentList, constructor.ContainingType, constructor);
            }
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, SymbolCache cache)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);

            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                // Check for ImmutableDictionary.Create<TKey, TValue>(), ImmutableDictionary.CreateRange<TKey, TValue>(), or ImmutableDictionary.CreateBuilder<TKey, TValue>()
                if (methodSymbol.ContainingType != null &&
                    cache.ImmutableDictionary != null &&
                    SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, cache.ImmutableDictionary) &&
                    (methodSymbol.Name == "Create" || methodSymbol.Name == "CreateRange" || methodSymbol.Name == "CreateBuilder") &&
                    methodSymbol.IsStatic &&
                    methodSymbol.TypeArguments.Length == 2)
                {
                    var keyType = methodSymbol.TypeArguments[0];

                    // Check if the key type is string
                    if (keyType.SpecialType == SpecialType.System_String)
                    {
                        // Check if a StringComparer argument is provided
                        if (invocation.ArgumentList != null && HasStringComparerArgument(context, cache, invocation.ArgumentList))
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
                        if (invocation.ArgumentList != null && HasStringComparerArgument(context, cache, invocation.ArgumentList))
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

        private static void AnalyzeDictionaryCreation(SyntaxNodeAnalysisContext context, SymbolCache cache, SyntaxNode node, ArgumentListSyntax? argumentList, ITypeSymbol typeSymbol, IMethodSymbol calledConstructor)
        {
            // Check if it's a dictionary type
            if (!IsDictionaryType(typeSymbol, cache, out var keyType))
            {
                return;
            }

            // Check if the key type is string
            if (keyType?.SpecialType != SpecialType.System_String)
            {
                return;
            }

            // Check if a StringComparer argument is provided
            if (argumentList != null && HasStringComparerArgument(context, cache, argumentList))
            {
                return;
            }

            // Check if the type has a constructor overload that accepts an IEqualityComparer<TKey>
            // If no such overload exists, the type cannot accept a comparer, so don't report a diagnostic
            if (!HasComparerConstructorOverload(typeSymbol, keyType, cache))
            {
                return;
            }

            // Report diagnostic
            var diagnostic = Diagnostic.Create(Rule, node.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsDictionaryType(ITypeSymbol typeSymbol, SymbolCache cache, out ITypeSymbol? keyType)
        {
            keyType = null;

            if (typeSymbol is not INamedTypeSymbol namedType)
                return false;

            // Check for Dictionary<TKey, TValue>
            if (IsDictionaryInterface(namedType, cache))
            {
                keyType = namedType.TypeArguments.FirstOrDefault();
                return true;
            }

            // Check for types implementing IDictionary<TKey, TValue>
            foreach (var @interface in namedType.AllInterfaces)
            {
                if (IsDictionaryInterface(@interface, cache))
                {
                    keyType = @interface.TypeArguments.FirstOrDefault();
                    return true;
                }
            }

            return false;
        }

        private static bool IsDictionaryInterface(INamedTypeSymbol typeSymbol, SymbolCache cache)
        {
            if (!typeSymbol.IsGenericType || typeSymbol.TypeArguments.Length != 2)
            {
                return false;
            }

            var originalDefinition = typeSymbol.OriginalDefinition;
            return (cache.IDictionary2 != null && SymbolEqualityComparer.Default.Equals(originalDefinition, cache.IDictionary2)) ||
                   (cache.Dictionary2 != null && SymbolEqualityComparer.Default.Equals(originalDefinition, cache.Dictionary2)) ||
                   (cache.ConcurrentDictionary2 != null && SymbolEqualityComparer.Default.Equals(originalDefinition, cache.ConcurrentDictionary2));
        }

        private static bool HasComparerConstructorOverload(ITypeSymbol typeSymbol, ITypeSymbol keyType, SymbolCache cache)
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
                        var original = parameterType.OriginalDefinition;
                        var isEquality = cache.IEqualityComparer1 != null && SymbolEqualityComparer.Default.Equals(original, cache.IEqualityComparer1);
                        var isComparer = cache.IComparer1 != null && SymbolEqualityComparer.Default.Equals(original, cache.IComparer1);
                        if (isEquality || isComparer)
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

        private static bool HasStringComparerArgument(SyntaxNodeAnalysisContext context, SymbolCache cache, ArgumentListSyntax argumentList)
        {
            foreach (var argument in argumentList.Arguments)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken);
                if (typeInfo.Type != null)
                {
                    if (cache.StringComparer != null && SymbolEqualityComparer.Default.Equals(typeInfo.Type, cache.StringComparer))
                    {
                        return true;
                    }

                    if (IsIEqualityComparerOfString(typeInfo.Type, cache) || IsIComparerOfString(typeInfo.Type, cache))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsIEqualityComparerOfString(ITypeSymbol typeSymbol, SymbolCache cache)
        {
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                if (namedType.IsGenericType &&
                    cache.IEqualityComparer1 != null && SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, cache.IEqualityComparer1) &&
                    namedType.TypeArguments.Length == 1 &&
                    namedType.TypeArguments[0].SpecialType == SpecialType.System_String)
                {
                    return true;
                }

                foreach (var @interface in namedType.AllInterfaces)
                {
                    if (@interface.IsGenericType &&
                        cache.IEqualityComparer1 != null && SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, cache.IEqualityComparer1) &&
                        @interface.TypeArguments.Length == 1 &&
                        @interface.TypeArguments[0].SpecialType == SpecialType.System_String)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsIComparerOfString(ITypeSymbol typeSymbol, SymbolCache cache)
        {
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                if (namedType.IsGenericType &&
                    cache.IComparer1 != null && SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, cache.IComparer1) &&
                    namedType.TypeArguments.Length == 1 &&
                    namedType.TypeArguments[0].SpecialType == SpecialType.System_String)
                {
                    return true;
                }

                foreach (var @interface in namedType.AllInterfaces)
                {
                    if (@interface.IsGenericType &&
                        cache.IComparer1 != null && SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, cache.IComparer1) &&
                        @interface.TypeArguments.Length == 1 &&
                        @interface.TypeArguments[0].SpecialType == SpecialType.System_String)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private sealed class SymbolCache
        {
            public INamedTypeSymbol? IDictionary2 { get; set; }
            public INamedTypeSymbol? Dictionary2 { get; set; }
            public INamedTypeSymbol? ConcurrentDictionary2 { get; set; }
            public INamedTypeSymbol? IEqualityComparer1 { get; set; }
            public INamedTypeSymbol? IComparer1 { get; set; }
            public INamedTypeSymbol? StringComparer { get; set; }
            public INamedTypeSymbol? ImmutableDictionary { get; set; }
        }
    }
}
