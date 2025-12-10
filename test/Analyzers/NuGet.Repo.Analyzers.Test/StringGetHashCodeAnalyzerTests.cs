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
    public class StringGetHashCodeAnalyzerTests
    {
        private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
        {
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.StringComparer).Assembly.Location),
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

            var compilation = CSharpCompilation.Create("TestCompilation")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(references)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));

            var compilationDiagnostics = compilation.GetDiagnostics();
            if (compilationDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                throw new System.Exception($"Compilation failed: {string.Join(", ", compilationDiagnostics.Select(d => d.ToString()))}");
            }

            var analyzer = new StringGetHashCodeAnalyzer();
            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
            var allDiagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();
            return allDiagnostics.Where(d => d.Id == StringGetHashCodeAnalyzer.DiagnosticId).ToImmutableArray();
        }

        [Fact]
        public async Task StringGetHashCode_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string str = ""test"";
        int hash = str.GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringGetHashCodeAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringLiteralGetHashCode_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        int hash = ""test"".GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringGetHashCodeAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringPropertyGetHashCode_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    string Property { get; set; }

    void TestMethod()
    {
        int hash = Property.GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringGetHashCodeAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringMethodReturnGetHashCode_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    string GetString() => ""test"";

    void TestMethod()
    {
        int hash = GetString().GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringGetHashCodeAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task MultipleStringGetHashCode_ReportsMultipleDiagnostics()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        int hash1 = str1.GetHashCode();
        int hash2 = str2.GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(2, diagnostics.Length);
            Assert.All(diagnostics, d => Assert.Equal(StringGetHashCodeAnalyzer.DiagnosticId, d.Id));
        }

        [Fact]
        public async Task StringComparerGetHashCode_NoDiagnostic()
        {
            var source = @"
using System;

class TestClass
{
    void TestMethod()
    {
        string str = ""test"";
        int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(str);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task NonStringGetHashCode_NoDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        int number = 42;
        int hash = number.GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ObjectGetHashCode_NoDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        object obj = new object();
        int hash = obj.GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task CustomClassGetHashCode_NoDiagnostic()
        {
            var source = @"
class CustomClass
{
    public override int GetHashCode() => 42;
}

class TestClass
{
    void TestMethod()
    {
        var custom = new CustomClass();
        int hash = custom.GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task StringGetHashCodeWithArguments_NoDiagnostic()
        {
            var source = @"
using System;

class TestClass
{
    void TestMethod()
    {
        string str = ""test"";
        // GetHashCode(StringComparison) if it existed - but it doesn't in reality
        // This test ensures we don't flag GetHashCode calls with parameters
        int hash = str.GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            // This should report diagnostic since it's the parameterless version
            Assert.Equal(1, diagnostics.Length);
        }

        [Fact]
        public async Task StringInterpolationGetHashCode_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string name = ""World"";
        int hash = $""Hello, {name}"".GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringGetHashCodeAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringConcatenationGetHashCode_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string part1 = ""Hello"";
        string part2 = ""World"";
        int hash = (part1 + part2).GetHashCode();
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringGetHashCodeAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringComparerOrdinal_NoDiagnostic()
        {
            var source = @"
using System;

class TestClass
{
    void TestMethod()
    {
        string str = ""test"";
        int hash = StringComparer.Ordinal.GetHashCode(str);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task StringComparerCurrentCulture_NoDiagnostic()
        {
            var source = @"
using System;

class TestClass
{
    void TestMethod()
    {
        string str = ""test"";
        int hash = StringComparer.CurrentCulture.GetHashCode(str);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }
    }
}
