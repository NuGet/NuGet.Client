// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Build.Tasks.Pack.Test
{
    public class GetPackOutputItemsTaskTests
    {
        public static IEnumerable<object[]> PackageFileNameTestCases => PackageFileNameTestCase.TestCases;

        // This unit test verifies that GetPackOutputItemsTask outputs the expected file name.
        [Theory]
        [MemberData(nameof(PackageFileNameTestCases))]
        public void GetPackOutputItemsTaskTests_Execute_CheckPackageFileName(PackageFileNameTestCase testCase)
        {
            var outputItemTask = new GetPackOutputItemsTask
            {
                PackageId = PackageFileNameTestCase.IdProjProp,
                PackageVersion = testCase.VersionProjProp,
                IncludeSymbols = testCase.IncludeSymbols,
                SymbolPackageFormat = PackageFileNameTestsCommon.GetSymbolPackageFormatText(testCase.SymbolPackageFormat),
                OutputFileNamesWithoutVersion = testCase.OutputFileNamesWithoutVersion
            };

            var nuspecProps = new List<string>();
            if (!string.IsNullOrWhiteSpace(testCase.VersionNuspecProperties))
            {
                nuspecProps.Add($"version={testCase.VersionNuspecProperties}");
            }
            if (!string.IsNullOrWhiteSpace(testCase.IdNuspecProperties))
            {
                nuspecProps.Add($"id={testCase.IdNuspecProperties}");
            }
            if (nuspecProps.Count > 0)
            {
                outputItemTask.NuspecProperties = nuspecProps.ToArray();
            }

            using var testDirectory = TestDirectory.Create();
            outputItemTask.PackageOutputPath = testDirectory.Path;
            outputItemTask.NuspecOutputPath = testDirectory.Path;
            if (testCase.UseNuspecFile)
            {
                outputItemTask.NuspecFile = Path.Combine(testDirectory.Path, PackageFileNameTestsCommon.FILENAME_NUSPEC_FILE);
            }

            PackageFileNameTestsCommon.CreateNuspecFile(testCase, testDirectory);

            Assert.True(outputItemTask.Execute());

            // GetPackOutputItemsTask always emits the primary .nupkg (and its .nuspec) plus, when symbols
            // are requested, the symbols package (and its .nuspec). Assert on the exact set of package files
            // so the test also fails if the task emits an unexpected extra package, not only a missing one.
            string[] actualPackageFiles = outputItemTask.OutputPackItems
                .Select(item => Path.GetFileName(item.ItemSpec))
                .Where(name => name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            actualPackageFiles.Should().BeEquivalentTo(testCase.OutputNupkgNames);
        }

        // Regression test: when the nuspec file has no explicit <id> element and the id comes from
        // NuspecProperties / the project's PackageId property, GetPackOutputItemsTask must resolve
        // the id correctly (i.e. fall back to the PackageId MSBuild property rather than using null).
        [Fact]
        public void GetPackOutputItemsTaskTests_Execute_NuspecFileHasNoId_FallsBackToPackageId()
        {
            using var testDirectory = TestDirectory.Create();

            // Write a nuspec that deliberately omits the <id> element.
            var nuspecPath = Path.Combine(testDirectory.Path, "test.nuspec");
            File.WriteAllText(nuspecPath, """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
                  <metadata>
                    <version>1.0.0</version>
                    <authors>Unit Test</authors>
                    <description>Sample Description</description>
                  </metadata>
                </package>
                """);

            var outputItemTask = new GetPackOutputItemsTask
            {
                PackageId = "MyPackage",
                PackageVersion = "1.0.0",
                PackageOutputPath = testDirectory.Path,
                NuspecOutputPath = testDirectory.Path,
                NuspecFile = nuspecPath,
                // Simulate the real-world scenario where the user declares the id via NuspecProperties.
                NuspecProperties = ["id=MyPackage"],
            };

            Assert.True(outputItemTask.Execute());

            string[] actualPackageFiles = outputItemTask.OutputPackItems
                .Select(item => Path.GetFileName(item.ItemSpec))
                .Where(name => name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // The id must come from the PackageId property ("MyPackage"), not from the missing nuspec element (null).
            actualPackageFiles.Should().BeEquivalentTo(new[] { "MyPackage.1.0.0.nupkg" });
        }

        [Fact]
        public void GetPackOutputItemsTaskTests_Execute_NuspecFileDoesNotExist_FallsBackToProjectProperties()
        {
            using var testDirectory = TestDirectory.Create();

            var outputItemTask = new GetPackOutputItemsTask
            {
                PackageId = "MyPackage",
                PackageVersion = "1.2.3",
                PackageOutputPath = testDirectory.Path,
                NuspecOutputPath = testDirectory.Path,
                SymbolPackageFormat = "snupkg",
                // Point at a nuspec path that does not exist on disk (mirrors the source-build scenario).
                NuspecFile = Path.Combine(testDirectory.Path, "does-not-exist.nuspec")
            };

            Assert.True(outputItemTask.Execute());

            string[] actualPackageFiles = outputItemTask.OutputPackItems
                .Select(item => Path.GetFileName(item.ItemSpec))
                .Where(name => name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            actualPackageFiles.Should().BeEquivalentTo(new[] { "MyPackage.1.2.3.nupkg" });
        }

        // Token substitution: when nuspec has <id>$id$</id>, the $id$ token must be replaced
        // with the value supplied via NuspecProperties (e.g. "id=ResolvedPackage").
        [Fact]
        public void GetPackOutputItemsTaskTests_Execute_NuspecIdIsToken_SubstitutedFromNuspecProperties()
        {
            using var testDirectory = TestDirectory.Create();

            var nuspecPath = Path.Combine(testDirectory.Path, "test.nuspec");
            File.WriteAllText(nuspecPath, """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
                  <metadata>
                    <id>$id$</id>
                    <version>1.0.0</version>
                    <authors>Unit Test</authors>
                    <description>Sample Description</description>
                  </metadata>
                </package>
                """);

            var outputItemTask = new GetPackOutputItemsTask
            {
                PackageId = "ProjectPackageId",
                PackageVersion = "1.0.0",
                PackageOutputPath = testDirectory.Path,
                NuspecOutputPath = testDirectory.Path,
                NuspecFile = nuspecPath,
                NuspecProperties = ["id=ResolvedPackage"],
            };

            Assert.True(outputItemTask.Execute());

            string[] actualPackageFiles = outputItemTask.OutputPackItems
                .Select(item => Path.GetFileName(item.ItemSpec))
                .Where(name => name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // The $id$ token must be replaced with the value from NuspecProperties.
            actualPackageFiles.Should().BeEquivalentTo(new[] { "ResolvedPackage.1.0.0.nupkg" });
        }

        // Token substitution: when nuspec has <id>$id$</id> and NuspecProperties does not provide
        // an id value, the task must fall back to the project's PackageId property.
        [Fact]
        public void GetPackOutputItemsTaskTests_Execute_NuspecIdIsToken_FallsBackToProjectPackageId()
        {
            using var testDirectory = TestDirectory.Create();

            var nuspecPath = Path.Combine(testDirectory.Path, "test.nuspec");
            File.WriteAllText(nuspecPath, """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
                  <metadata>
                    <id>$id$</id>
                    <version>1.0.0</version>
                    <authors>Unit Test</authors>
                    <description>Sample Description</description>
                  </metadata>
                </package>
                """);

            var outputItemTask = new GetPackOutputItemsTask
            {
                PackageId = "ProjectPackageId",
                PackageVersion = "1.0.0",
                PackageOutputPath = testDirectory.Path,
                NuspecOutputPath = testDirectory.Path,
                NuspecFile = nuspecPath,
                // No NuspecProperties for "id" — the $id$ token must fall back to PackageId.
            };

            Assert.True(outputItemTask.Execute());

            string[] actualPackageFiles = outputItemTask.OutputPackItems
                .Select(item => Path.GetFileName(item.ItemSpec))
                .Where(name => name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            actualPackageFiles.Should().BeEquivalentTo(new[] { "ProjectPackageId.1.0.0.nupkg" });
        }

        // Token substitution: when nuspec has <version>$version$</version> and NuspecProperties does
        // not supply a version, the task must fall back to the project's PackageVersion property.
        [Fact]
        public void GetPackOutputItemsTaskTests_Execute_NuspecVersionIsToken_FallsBackToProjectPackageVersion()
        {
            using var testDirectory = TestDirectory.Create();

            var nuspecPath = Path.Combine(testDirectory.Path, "test.nuspec");
            File.WriteAllText(nuspecPath, """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
                  <metadata>
                    <id>MyPackage</id>
                    <version>$version$</version>
                    <authors>Unit Test</authors>
                    <description>Sample Description</description>
                  </metadata>
                </package>
                """);

            var outputItemTask = new GetPackOutputItemsTask
            {
                PackageId = "MyPackage",
                PackageVersion = "2.3.4",
                PackageOutputPath = testDirectory.Path,
                NuspecOutputPath = testDirectory.Path,
                NuspecFile = nuspecPath,
                // No NuspecProperties for "version" — the $version$ token must fall back to PackageVersion.
            };

            Assert.True(outputItemTask.Execute());

            string[] actualPackageFiles = outputItemTask.OutputPackItems
                .Select(item => Path.GetFileName(item.ItemSpec))
                .Where(name => name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            actualPackageFiles.Should().BeEquivalentTo(new[] { "MyPackage.2.3.4.nupkg" });
        }
    }
}
