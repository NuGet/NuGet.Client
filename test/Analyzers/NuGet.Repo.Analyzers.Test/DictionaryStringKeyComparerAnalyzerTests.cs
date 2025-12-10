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
    public class DictionaryStringKeyComparerAnalyzerTests
    {
        private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
        {
            // Get all necessary references for the compilation
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.Dictionary<,>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Concurrent.ConcurrentDictionary<,>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.SortedDictionary<,>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Immutable.ImmutableDictionary).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEqualityComparer<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            };

#if NET
            // Add IsExternalInit for C# 9+ features (only available in .NET 5+)
            try
            {
                var isExternalInitType = typeof(System.Runtime.CompilerServices.IsExternalInit);
                references.Add(MetadataReference.CreateFromFile(isExternalInitType.Assembly.Location));
            }
            catch
            {
                // IsExternalInit not available, skip it
            }
#endif

            // Add System.Runtime reference for .NET Core/.NET 5+ scenarios
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
                // System.Runtime not available as a separate assembly, skip it
            }

            var compilation = CSharpCompilation.Create("TestCompilation")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(references)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));

            // Ensure the compilation is successful
            var compilationDiagnostics = compilation.GetDiagnostics();
            if (compilationDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                throw new System.Exception($"Compilation failed: {string.Join(", ", compilationDiagnostics.Select(d => d.ToString()))}");
            }

            var analyzer = new DictionaryStringKeyComparerAnalyzer();
            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
            var allDiagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();
            return allDiagnostics.Where(d => d.Id == DictionaryStringKeyComparerAnalyzer.DiagnosticId).ToImmutableArray();
        }

        [Fact]
        public async Task Dictionary_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var dict = new Dictionary<string, int>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(DictionaryStringKeyComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task Dictionary_WithStringKey_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task Dictionary_WithIntKey_NoComparer_NoDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var dict = new Dictionary<int, string>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ConcurrentDictionary_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Concurrent;

class TestClass
{
    void TestMethod()
    {
        var dict = new ConcurrentDictionary<string, int>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
        }

        [Fact]
        public async Task IDictionary_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        IDictionary<string, int> dict = new Dictionary<string, int>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
        }

        [Fact]
        public async Task ImplicitNew_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        Dictionary<string, int> dict = new();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
        }

        [Fact]
        public async Task ImplicitNew_WithStringKey_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        Dictionary<string, int> dict = new(StringComparer.Ordinal);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task Dictionary_WithCapacity_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var dict = new Dictionary<string, int>(10);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
        }

        [Fact]
        public async Task Dictionary_WithCapacityAndComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var dict = new Dictionary<string, int>(10, StringComparer.OrdinalIgnoreCase);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task SortedDictionary_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var dict = new SortedDictionary<string, int>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(DictionaryStringKeyComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task SortedDictionary_WithStringKey_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var dict = new SortedDictionary<string, int>(StringComparer.Ordinal);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ImmutableDictionary_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var dict = ImmutableDictionary.Create<string, int>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(DictionaryStringKeyComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ImmutableDictionary_WithStringKey_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var dict = ImmutableDictionary.Create<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ImmutableDictionary_CreateRange_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var items = new[] { new KeyValuePair<string, int>(""a"", 1) };
        var dict = ImmutableDictionary.CreateRange<string, int>(items);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(DictionaryStringKeyComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ImmutableDictionary_CreateRange_WithStringKey_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var items = new[] { new KeyValuePair<string, int>(""a"", 1) };
        var dict = ImmutableDictionary.CreateRange<string, int>(StringComparer.Ordinal, items);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ImmutableDictionary_WithIntKey_NoComparer_NoDiagnostic()
        {
            var source = @"
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var dict = ImmutableDictionary.Create<int, string>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ImmutableDictionaryBuilder_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, int>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(DictionaryStringKeyComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ImmutableDictionaryBuilder_WithStringKey_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ImmutableDictionaryBuilder_WithIntKey_NoComparer_NoDiagnostic()
        {
            var source = @"
using System.Collections.Immutable;

class TestClass
{
    void TestMethod()
    {
        var builder = ImmutableDictionary.CreateBuilder<int, string>();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ToDictionary_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new[] { ""a"", ""b"", ""c"" };
        var dict = items.ToDictionary(x => x, x => x.Length);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(DictionaryStringKeyComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ToDictionary_WithStringKey_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new[] { ""a"", ""b"", ""c"" };
        var dict = items.ToDictionary(x => x, x => x.Length, StringComparer.OrdinalIgnoreCase);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ToDictionary_WithIntKey_NoComparer_NoDiagnostic()
        {
            var source = @"
using System.Collections.Generic;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new[] { 1, 2, 3 };
        var dict = items.ToDictionary(x => x, x => x.ToString());
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ToDictionary_KeyOnly_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Generic;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new[] { new { Key = ""a"", Value = 1 } };
        var dict = items.ToDictionary(x => x.Key);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(DictionaryStringKeyComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ToDictionary_KeyOnly_WithStringKey_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new[] { new { Key = ""a"", Value = 1 } };
        var dict = items.ToDictionary(x => x.Key, StringComparer.Ordinal);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ToImmutableDictionary_WithStringKey_NoComparer_ReportsDiagnostic()
        {
            var source = @"
using System.Collections.Immutable;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new[] { ""a"", ""b"", ""c"" };
        var dict = items.ToImmutableDictionary(x => x, x => x.Length);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(DictionaryStringKeyComparerAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ToImmutableDictionary_WithStringKey_WithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Immutable;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new[] { ""a"", ""b"", ""c"" };
        var dict = items.ToImmutableDictionary(x => x, x => x.Length, StringComparer.Ordinal);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ToImmutableDictionary_WithIntKey_NoComparer_NoDiagnostic()
        {
            var source = @"
using System.Collections.Immutable;
using System.Linq;

class TestClass
{
    void TestMethod()
    {
        var items = new[] { 1, 2, 3 };
        var dict = items.ToImmutableDictionary(x => x, x => x.ToString());
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }

        [Fact]
        public async Task ReadOnlyDictionary_InnerDictionaryWithComparer_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

class TestClass
{
    void TestMethod()
    {
        var innerDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var readOnlyDict = new ReadOnlyDictionary<string, int>(innerDict);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(0, diagnostics.Length);
        }
    }
}
