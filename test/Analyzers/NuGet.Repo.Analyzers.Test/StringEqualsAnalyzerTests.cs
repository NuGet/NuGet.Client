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
    public class StringEqualsAnalyzerTests
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

            var analyzer = new StringEqualsAnalyzer();
            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
            var allDiagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();
            return allDiagnostics.Where(d => d.Id == StringEqualsAnalyzer.DiagnosticId).ToImmutableArray();
        }

        [Fact]
        public async Task StringEqualsOperator_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        bool result = str1 == str2;
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringEqualsAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringNotEqualsOperator_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        bool result = str1 != str2;
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringEqualsAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringLiteralEqualsOperator_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        bool result = ""test1"" == ""test2"";
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringEqualsAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringInstanceEquals_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        bool result = str1.Equals(str2);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringEqualsAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringStaticEquals_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        bool result = string.Equals(str1, str2);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringEqualsAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task ObjectStaticEqualsWithStrings_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        bool result = object.Equals(str1, str2);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringEqualsAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringEqualsWithProperty_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    string Property { get; set; }

    void TestMethod()
    {
        bool result = Property == ""test"";
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringEqualsAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task StringEqualsWithMethodReturn_ReportsDiagnostic()
        {
            var source = @"
class TestClass
{
    string GetString() => ""test"";

    void TestMethod()
    {
        bool result = GetString() == ""test"";
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(1, diagnostics.Length);
            Assert.Equal(StringEqualsAnalyzer.DiagnosticId, diagnostics[0].Id);
        }

        [Fact]
        public async Task MultipleStringComparisons_ReportsMultipleDiagnostics()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        string str3 = ""test3"";
        bool result1 = str1 == str2;
        bool result2 = str2.Equals(str3);
        bool result3 = str1 != str3;
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Equal(3, diagnostics.Length);
            Assert.All(diagnostics, d => Assert.Equal(StringEqualsAnalyzer.DiagnosticId, d.Id));
        }

        [Fact]
        public async Task StringEqualsWithStringComparison_NoDiagnostic()
        {
            var source = @"
using System;

class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        bool result = str1.Equals(str2, StringComparison.OrdinalIgnoreCase);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task StringStaticEqualsWithStringComparison_NoDiagnostic()
        {
            var source = @"
using System;

class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        bool result = string.Equals(str1, str2, StringComparison.Ordinal);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task StringComparerEquals_NoDiagnostic()
        {
            var source = @"
using System;

class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        bool result = StringComparer.OrdinalIgnoreCase.Equals(str1, str2);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task IntEqualsOperator_NoDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        int num1 = 1;
        int num2 = 2;
        bool result = num1 == num2;
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task IntInstanceEquals_NoDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        int num1 = 1;
        int num2 = 2;
        bool result = num1.Equals(num2);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ObjectEqualsNonString_NoDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        object obj1 = 1;
        object obj2 = 2;
        bool result = object.Equals(obj1, obj2);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task CustomClassEqualsOperator_NoDiagnostic()
        {
            var source = @"
class CustomClass
{
    public static bool operator ==(CustomClass left, CustomClass right) => true;
    public static bool operator !=(CustomClass left, CustomClass right) => false;
}

class TestClass
{
    void TestMethod()
    {
        CustomClass obj1 = new CustomClass();
        CustomClass obj2 = new CustomClass();
        bool result = obj1 == obj2;
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task IEqualityComparerEquals_NoDiagnostic()
        {
            var source = @"
using System;
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        string str1 = ""test1"";
        string str2 = ""test2"";
        IEqualityComparer<string> comparer = StringComparer.OrdinalIgnoreCase;
        bool result = comparer.Equals(str1, str2);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task StringComparerEqualsWithNull_NoDiagnostic()
        {
            var source = @"
using System;

class TestClass
{
    void TestMethod()
    {
        string str = ""test"";
        bool result = StringComparer.Ordinal.Equals(str, null);
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task StringEqualsOperatorWithNull_NoDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string str = ""test"";
        bool result = str == null;
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task StringNotEqualsOperatorWithNull_NoDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string str = ""test"";
        bool result = str != null;
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task StringEqualsOperatorWithStringEmpty_NoDiagnostic()
        {
            var source = @"
class TestClass
{
    void TestMethod()
    {
        string value = """";
        bool result = value == string.Empty;
    }
}";

            var diagnostics = await GetDiagnosticsAsync(source);
            Assert.Empty(diagnostics);
        }
    }
}
