// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace NuGet.Repo.Analyzers.Test
{
    public class HashSetStringComparerAnalyzerTests
    {
        private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
        {
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.HashSet<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Immutable.ImmutableHashSet).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEqualityComparer<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            };

#if NET
            try
            {
                var isExternalInitType = typeof(System.Runtime.CompilerServices.IsExternalInit);
                references.Add(MetadataReference.CreateFromFile(isExternalInitType.Assembly.Location));
            }
            catch
            {
            }
#endif

            try
            {
                var systemRuntimeAssembly = System.Reflection.Assembly.Load("System.Runtime");
                if (systemRuntimeAssembly != null)
                {
                    references.Add(MetadataReference.CreateFromFile(systemRuntimeAssembly.Location));
                }
            }
            catch
            {
            }

            try
            {
                var systemCollectionsAssembly = System.Reflection.Assembly.Load("System.Collections");
                if (systemCollectionsAssembly != null)
                {
                    references.Add(MetadataReference.CreateFromFile(systemCollectionsAssembly.Location));
                }
            }
            catch
            {
            }

            var compilation = CSharpCompilation.Create("TestCompilation")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(references)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));

            var compilationDiagnostics = compilation.GetDiagnostics();
            if (compilationDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                throw new System.Exception($"Compilation failed: {string.Join(", ", compilationDiagnostics.Select(d => d.ToString()))}");
            }

            var analyzer = new HashSetStringComparerAnalyzer();
            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
            var allDiagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();
            return allDiagnostics.Where(d => d.Id == HashSetStringComparerAnalyzer.DiagnosticId).ToImmutableArray();
        }

        [Fact]
        public async Task HashSet_WithStringElement_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var set = new HashSet<string>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(HashSetStringComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task HashSet_WithStringElement_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task HashSet_WithIntElement_NoComparer_NoDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var set = new HashSet<int>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task HashSet_WithCollection_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var items = new List<string>();
        var set = new HashSet<string>(items);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(HashSetStringComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task HashSet_WithCollection_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var items = new List<string>();
        var set = new HashSet<string>(items, StringComparer.Ordinal);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ImplicitHashSet_WithStringElement_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        HashSet<string> set = new();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(HashSetStringComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ImplicitHashSet_WithStringElement_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ToHashSet_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new List<string>();
        var set = items.ToHashSet();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(HashSetStringComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ToHashSet_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new List<string>();
        var set = items.ToHashSet(StringComparer.Ordinal);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ToHashSet_IntElement_NoComparer_NoDiagnostic()
        {
            var source = @"
using System.Collections.Generic;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new List<int>();
        var set = items.ToHashSet();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ImmutableHashSet_Create_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var set = ImmutableHashSet.Create<string>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(HashSetStringComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ImmutableHashSet_Create_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var set = ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ImmutableHashSet_CreateRange_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var items = new List<string>();
        var set = ImmutableHashSet.CreateRange<string>(items);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(HashSetStringComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ImmutableHashSet_CreateRange_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var items = new List<string>();
        var set = ImmutableHashSet.CreateRange<string>(StringComparer.Ordinal, items);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ImmutableHashSet_CreateBuilder_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var builder = ImmutableHashSet.CreateBuilder<string>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(HashSetStringComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ImmutableHashSet_CreateBuilder_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ToImmutableHashSet_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new List<string>();
        var set = items.ToImmutableHashSet();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(HashSetStringComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ToImmutableHashSet_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new List<string>();
        var set = items.ToImmutableHashSet(StringComparer.Ordinal);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task MultipleViolations_ReportMultipleDiagnostics()
        {
            var source = @"
using System.Collections.Generic;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var set1 = new HashSet<string>();
        var set2 = new HashSet<string>();
        var items = new List<string>();
        var set3 = items.ToHashSet();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(3, diagnostics.Length);
        }

        [Fact]
        public async Task HashSet_WithCustomIEqualityComparerImplementation_NoDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class CustomStringComparer : IEqualityComparer<string>
{
    public bool Equals(string x, string y)
    {
        return string.Equals(x, y, System.StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(string obj)
    {
        return obj.ToUpperInvariant().GetHashCode();
    }
}

class TestClass
{
    void TestMethod()
    {
        var comparer = new CustomStringComparer();
        var set = new HashSet<string>(comparer);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }
    }
}
