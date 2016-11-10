// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Build.Tasks.Pack.Test
{
    public class PackTaskLogicTests
    {
        [Fact]
        public void PackTaskLogic_ProducesBasicPackage()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);

                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.True(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the .nuspec.
                    Assert.Equal(tc.Request.PackageId, nuspecReader.GetId());
                    Assert.Equal(tc.Request.PackageVersion, nuspecReader.GetVersion().ToFullString());
                    Assert.Equal(string.Join(",", tc.Request.Authors), nuspecReader.GetAuthors());
                    Assert.Equal(string.Join(",", tc.Request.Authors), nuspecReader.GetOwners());
                    Assert.Equal(tc.Request.Description, nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    Assert.Equal(1, dependencyGroups.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packages.Count);
                    Assert.Equal("Newtonsoft.Json", packages[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("8.0.1")), packages[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packages[0].Exclude);
                    Assert.Empty(packages[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/net45/a.dll" }, libItems[0].Items);
                }
            }
        }

        [Fact]
        public void PackTaskLogic_SplitsTags()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);
                tc.Request.Tags = new[]
                {
                    "tagA",
                    "  tagB ",
                    null,
                    "tagC1;tagC2",
                    "tagD1,tagD2",
                    "tagE1 tagE2"
                };

                // Act
                tc.BuildPackage();

                // Assert
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    Assert.Equal("tagA   tagB   tagC1;tagC2 tagD1,tagD2 tagE1 tagE2", nuspecReader.GetTags());
                }
            }
        }

        [Fact]
        public void PackTaskLogic_CanSkipProducingTheNupkg()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);
                tc.Request.ContinuePackingAfterGeneratingNuspec = false;

                // Act
                tc.BuildPackage();

                // Assert
                Assert.True(File.Exists(tc.NuspecPath), "The intermediate .nuspec file is not in the expected place.");
                Assert.False(File.Exists(tc.NupkgPath), "The output .nupkg file is not in the expected place.");
            }
        }

        [Fact]
        public void PackTaskLogic_SupportsMultipleFrameworks()
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);
                tc.Request.TargetPathsToAssemblies = new string[0];
                tc.Request.TargetFrameworks = new[] { "netcoreapp1.0", "net45" };
                tc.Request.PackageReferences = new[]
                {
                    new MSBuildItem("Newtonsoft.Json", new Dictionary<string, string>
                    {
                        { "Version", "9.0.1" },
                        { "TargetFramework", "netcoreapp1.0" }
                    }),
                    new MSBuildItem("NuGet.Versioning", new Dictionary<string, string>
                    {
                        { "Version", "3.3.0" },
                        { "TargetFramework", "net45" }
                    })
                };

                // Act
                tc.BuildPackage();

                // Assert
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    
                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework, new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(2, dependencyGroups.Count);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp10, dependencyGroups[0].TargetFramework);
                    var packagesA = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packagesA.Count);
                    Assert.Equal("Newtonsoft.Json", packagesA[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("9.0.1")), packagesA[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesA[0].Exclude);
                    Assert.Empty(packagesA[0].Include);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, dependencyGroups[1].TargetFramework);
                    var packagesB = dependencyGroups[1].Packages.ToList();
                    Assert.Equal(1, packagesB.Count);
                    Assert.Equal("NuGet.Versioning", packagesB[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("3.3.0")), packagesB[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesB[0].Exclude);
                    Assert.Empty(packagesB[0].Include);
                }
            }
        }

        [Theory]
        [InlineData(null,              null,     null,            true,  "",                                            "Analyzers,Build")]
        [InlineData(null,              "Native", null,            true,  "",                                            "Analyzers,Build,Native")]
        [InlineData("Compile",         null,     null,            true,  "",                                            "Analyzers,Build,Native,Runtime")]
        [InlineData("Compile;Runtime", null,     null,            true,  "",                                            "Analyzers,Build,Native")]
        [InlineData("All",             null,     "None",          true,  "All",                                         "")]
        [InlineData("All",             null,     "Compile",       true,  "Analyzers,Build,ContentFiles,Native,Runtime", "")]
        [InlineData("All",             null,     "Compile;Build", true,  "Analyzers,ContentFiles,Native,Runtime",       "")]
        [InlineData("All",             "Native", "Compile;Build", true,  "Analyzers,ContentFiles,Runtime",              "")]
        [InlineData("All",             "Native", "Native;Build",  true,  "Analyzers,Compile,ContentFiles,Runtime",      "")]
        [InlineData("Compile",         "Native", "Native;Build",  true,  "",                                            "Analyzers,Build,Native,Runtime")]
        [InlineData("All",             "All",    null,            false, null,                                          null)]
        [InlineData("Compile;Runtime", "All",    null,            false, null,                                          null)]
        [InlineData(null,              null,     "All",           false, null,                                          null)]
        public void PackTaskLogic_SupportsIncludeExcludePrivateAssets_OnPackages(
            string includeAssets,
            string excludeAssets,
            string privateAssets,
            bool hasPackage,
            string expectedInclude,
            string expectedExclude)
        {
            // Arrange
            using (var testDir = TestDirectory.Create())
            {
                var tc = new TestContext(testDir);
                tc.Request.PackageReferences = new[]
                {
                    new MSBuildItem("NuGet.Versioning", new Dictionary<string, string>
                    {
                        { "Version", "3.3.0" },
                        { "TargetFramework", "net45" },
                        { "IncludeAssets", includeAssets },
                        { "ExcludeAssets", excludeAssets },
                        { "PrivateAssets", privateAssets },
                    })
                };

                // Act
                tc.BuildPackage();

                // Assert
                using (var nupkgReader = new PackageArchiveReader(tc.NupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    var package = nuspecReader
                        .GetDependencyGroups()
                        .SingleOrDefault()?
                        .Packages
                        .SingleOrDefault();

                    if (!hasPackage)
                    {
                        Assert.Null(package);
                    }
                    else
                    {
                        Assert.NotNull(package);
                        Assert.Equal(expectedInclude, string.Join(",", package.Include));
                        Assert.Equal(expectedExclude, string.Join(",", package.Exclude));
                    }
                }
            }
        }

        private class TestContext
        {
            public TestContext(TestDirectory testDir)
            {
                var fullPath = Path.Combine(testDir, "project.csproj");
                var rootDir = Path.GetPathRoot(testDir);
                var dllDir = Path.Combine(testDir, "bin", "Debug", "net45");
                var dllPath = Path.Combine(dllDir, "a.dll");

                Directory.CreateDirectory(dllDir);
                File.WriteAllBytes(dllPath, new byte[0]);

                TestDir = testDir;
                Request = new PackTaskRequest
                {
                    PackageId = "SomePackage",
                    PackageVersion = "3.0.0-beta",
                    Authors = new[] { "NuGet Team", "Unit test" },
                    Description = "A test package.",
                    PackItem = new MSBuildItem("project.csproj", new Dictionary<string, string>
                    {
                        { "RootDir", rootDir },
                        { "Directory", testDir.ToString().Substring(rootDir.Length) },
                        { "FileName", Path.GetFileNameWithoutExtension(fullPath) },
                        { "Extension", Path.GetExtension(fullPath) },
                        { "FullPath", fullPath }
                    }),
                    BuildOutputFolder = "lib",
                    NuspecOutputPath = "obj",
                    IncludeBuildOutput = true,
                    ContinuePackingAfterGeneratingNuspec = true,
                    TargetFrameworks = new[] { "net45" },
                    TargetPathsToAssemblies = new[] { dllPath },
                    Logger = new TestLogger(),
                    PackageReferences = new[]
                    {
                        new MSBuildItem("Newtonsoft.Json", new Dictionary<string, string>
                        {
                            { "Version", "8.0.1" },
                            { "TargetFramework", "net45" }
                        })
                    }
                };
            }

            public TestDirectory TestDir { get; }
            public PackTaskRequest Request { get; }

            public string NuspecPath
            {
                get
                {
                    return Path.Combine(
                        TestDir,
                        Request.NuspecOutputPath,
                        $"{Request.PackageId}.{Request.PackageVersion}.nuspec");
                }
            }

            public string NupkgPath
            {
                get
                {
                    return Path.Combine(
                        TestDir,
                        $"{Request.PackageId}.{Request.PackageVersion}.nupkg");
                }
            }

            public void BuildPackage()
            {
                // Arrange
                var target = new PackTaskLogic();

                // Act
                var packArgs = target.GetPackArgs(Request);
                var packageBuilder = target.GetPackageBuilder(Request);
                var runner = target.GetPackCommandRunner(Request, packArgs, packageBuilder);
                target.BuildPackage(runner);
            }
        }
    }
}
