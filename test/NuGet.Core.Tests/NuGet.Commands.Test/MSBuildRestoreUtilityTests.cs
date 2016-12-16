// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class MSBuildRestoreUtilityTests
    {
        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpecVersion_UAP()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project2Root = Path.Combine(workingDir, "b");

                var project1JsonPath = Path.Combine(project1Root, "project.json");
                var project2JsonPath = Path.Combine(project2Root, "project.json");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var project2Path = Path.Combine(project2Root, "b.csproj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectJsonPath", project1JsonPath },
                    { "ProjectName", "a" },
                    { "OutputType", "uap" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                });

                var project1Json = @"
                {
                    ""version"": ""2.0.0-beta.1+build"",
                    ""description"": """",
                    ""authors"": [ ""author"" ],
                    ""tags"": [ """" ],
                    ""projectUrl"": """",
                    ""licenseUrl"": """",
                    ""frameworks"": {
                        ""net45"": {
                        }
                    }
                }";

                Directory.CreateDirectory(project1Root);
                File.WriteAllText(project1JsonPath, project1Json);

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single(e => e.Name == "a");

                // Assert
                Assert.Equal("2.0.0-beta.1+build", project1Spec.Version.ToFullString());
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_Tool()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "OutputType", "DotnetCliTool" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "netcoreapp1.0" },
                    { "CrossTargeting", "true" },
                });

                // Package reference
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "x" },
                    { "VersionRange", "1.0.0-beta.*" },
                    { "TargetFrameworks", "netcoreapp1.0" },
                    { "CrossTargeting", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single(e => e.Name == "a");

                // Assert
                // Dependency counts
                Assert.Equal(1, project1Spec.GetTargetFramework(NuGetFramework.Parse("netcoreapp1.0")).Dependencies.Count);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyIncludeFlags()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46" },
                    { "CrossTargeting", "true" },
                });

                // Package references
                // A net46 -> X
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "x" },
                    { "VersionRange", "1.0.0" },
                    { "TargetFrameworks", "net46" },
                    { "IncludeAssets", "build;compile" },
                    { "CrossTargeting", "true" },
                });

                // A net46 -> Y
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "y" },
                    { "VersionRange", "1.0.0" },
                    { "TargetFrameworks", "net46" },
                    { "ExcludeAssets", "build;compile" },
                });

                // A net46 -> Z
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "z" },
                    { "VersionRange", "1.0.0" },
                    { "TargetFrameworks", "net46" },
                    { "PrivateAssets", "all" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();
                var x = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Single(e => e.Name == "x");
                var y = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Single(e => e.Name == "y");
                var z = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Single(e => e.Name == "z");

                // Assert
                // X
                Assert.Equal((LibraryIncludeFlags.Build | LibraryIncludeFlags.Compile), x.IncludeType);
                Assert.Equal((LibraryIncludeFlagUtils.DefaultSuppressParent), x.SuppressParent);

                // Y
                Assert.Equal(LibraryIncludeFlags.All & ~(LibraryIncludeFlags.Build | LibraryIncludeFlags.Compile), y.IncludeType);
                Assert.Equal((LibraryIncludeFlagUtils.DefaultSuppressParent), y.SuppressParent);

                // Z
                Assert.Equal(LibraryIncludeFlags.All, z.IncludeType);
                Assert.Equal(LibraryIncludeFlags.All, z.SuppressParent);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyBasicMetadata()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "Version", "2.0.0-rc.2+a.b.c" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                    { "RestoreLegacyPackagesDirectory", "true" }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(project1Path, project1Spec.FilePath);
                Assert.Equal("a", project1Spec.Name);
                Assert.Equal("2.0.0-rc.2+a.b.c", project1Spec.Version.ToFullString());
                Assert.Equal(ProjectStyle.PackageReference, project1Spec.RestoreMetadata.ProjectStyle);
                Assert.Equal("482C20DE-DFF9-4BD0-B90A-BD3201AA351A", project1Spec.RestoreMetadata.ProjectUniqueName);
                Assert.Equal(project1Path, project1Spec.RestoreMetadata.ProjectPath);
                Assert.Equal(0, project1Spec.RestoreMetadata.TargetFrameworks.SelectMany(e => e.ProjectReferences).Count());
                Assert.Null(project1Spec.RestoreMetadata.ProjectJsonPath);
                Assert.Equal("net46|netstandard1.6", string.Join("|", project1Spec.TargetFrameworks.Select(e => e.FrameworkName.GetShortFolderName())));
                Assert.Equal("net46|netstandard16", string.Join("|", project1Spec.RestoreMetadata.OriginalTargetFrameworks));
                Assert.Equal(outputPath1, project1Spec.RestoreMetadata.OutputPath);
                Assert.Equal("https://nuget.org/a/index.json|https://nuget.org/b/index.json", string.Join("|", project1Spec.RestoreMetadata.Sources.Select(s => s.Source)));
                Assert.Equal(fallbackFolder, string.Join("|", project1Spec.RestoreMetadata.FallbackFolders));
                Assert.Equal(packagesFolder, string.Join("|", project1Spec.RestoreMetadata.PackagesPath));
                Assert.Equal(0, project1Spec.RuntimeGraph.Runtimes.Count);
                Assert.Equal(0, project1Spec.RuntimeGraph.Supports.Count);
                Assert.True(project1Spec.RestoreMetadata.CrossTargeting);
                Assert.True(project1Spec.RestoreMetadata.LegacyPackagesDirectory);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyDefaultVersion()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                    { "RestoreLegacyPackagesDirectory", "true" }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal("1.0.0", project1Spec.Version.ToFullString());
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyInvalidVersionThrowsOnParse()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "Version", "notaversionstring" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                    { "RestoreLegacyPackagesDirectory", "true" }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act && Assert
                Assert.Throws(typeof(ArgumentException),
                    () => MSBuildRestoreUtility.GetDependencySpec(wrappedItems));
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreNonCrossTargeting()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(project1Path, project1Spec.FilePath);
                Assert.Equal("a", project1Spec.Name);
                Assert.Equal(ProjectStyle.PackageReference, project1Spec.RestoreMetadata.ProjectStyle);
                Assert.Equal("netstandard1.6", string.Join("|", project1Spec.TargetFrameworks.Select(e => e.FrameworkName.GetShortFolderName())));
                Assert.Equal("netstandard16", string.Join("|", project1Spec.RestoreMetadata.OriginalTargetFrameworks));
                Assert.False(project1Spec.RestoreMetadata.CrossTargeting);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreNonLegacyPackagesDirectory()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(project1Path, project1Spec.FilePath);
                Assert.Equal("a", project1Spec.Name);
                Assert.Equal(ProjectStyle.PackageReference, project1Spec.RestoreMetadata.ProjectStyle);
                Assert.Equal("netstandard1.6", string.Join("|", project1Spec.TargetFrameworks.Select(e => e.FrameworkName.GetShortFolderName())));
                Assert.Equal("netstandard16", string.Join("|", project1Spec.RestoreMetadata.OriginalTargetFrameworks));
                Assert.False(project1Spec.RestoreMetadata.LegacyPackagesDirectory);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyImports()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "TargetFrameworkInformation" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "PackageTargetFallback", "portable-net45+win8;dnxcore50;;" },
                    { "TargetFramework", "netstandard16" }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                var nsTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("netstandard16"));
                var netTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46"));

                // Assert
                Assert.Equal(2, nsTFM.Imports.Count);
                Assert.Equal(0, netTFM.Imports.Count);

                Assert.Equal(NuGetFramework.Parse("portable-net45+win8"), nsTFM.Imports[0]);
                Assert.Equal(NuGetFramework.Parse("dnxcore50"), nsTFM.Imports[1]);

                // Verify fallback framework
                var fallbackFramework = (FallbackFramework)project1Spec.TargetFrameworks
                    .Single(e => e.FrameworkName.GetShortFolderName() == "netstandard1.6")
                    .FrameworkName;

                // net46 does not have imports
                var fallbackFrameworkNet45 = project1Spec.TargetFrameworks
                    .Single(e => e.FrameworkName.GetShortFolderName() == "net46")
                    .FrameworkName
                    as FallbackFramework;

                Assert.Null(fallbackFrameworkNet45);
                Assert.Equal(2, fallbackFramework.Fallback.Count);
                Assert.Equal(NuGetFramework.Parse("portable-net45+win8"), fallbackFramework.Fallback[0]);
                Assert.Equal(NuGetFramework.Parse("dnxcore50"), fallbackFramework.Fallback[1]);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyImportsEmpty()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "TargetFrameworkInformation" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "PackageTargetFallback", "" },
                    { "TargetFramework", "netstandard16" }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                var nsTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("netstandard16"));
                var netTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46"));

                // Assert
                Assert.Equal(0, nsTFM.Imports.Count);
                Assert.Equal(0, netTFM.Imports.Count);

                // Verify no fallback frameworks
                var fallbackFrameworks = project1Spec.TargetFrameworks.Select(e => e.FrameworkName as FallbackFramework);
                Assert.True(fallbackFrameworks.All(e => e == null));
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyWhitespaceRemoved()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "  a\n  " },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "  net46  ;   netstandard16\n  " },
                    { "Sources", "https://nuget.org/a/index.json; https://nuget.org/b/index.json\n" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "TargetFrameworkInformation" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "PackageTargetFallback", "   portable-net45+win8  ;   dnxcore50\n   ; ;  " },
                    { "TargetFramework", " netstandard16\n  " }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                var nsTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("netstandard16"));
                var netTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46"));

                // Assert
                Assert.Equal("a", project1Spec.RestoreMetadata.ProjectName);
                Assert.Equal(2, nsTFM.Imports.Count);
                Assert.Equal(0, netTFM.Imports.Count);

                Assert.Equal(NuGetFramework.Parse("portable-net45+win8"), nsTFM.Imports[0]);
                Assert.Equal(NuGetFramework.Parse("dnxcore50"), nsTFM.Imports[1]);

                // Verify fallback framework
                var fallbackFramework = (FallbackFramework)project1Spec.TargetFrameworks
                    .Single(e => e.FrameworkName.GetShortFolderName() == "netstandard1.6")
                    .FrameworkName;

                // net46 does not have imports
                var fallbackFrameworkNet45 = project1Spec.TargetFrameworks
                    .Single(e => e.FrameworkName.GetShortFolderName() == "net46")
                    .FrameworkName
                    as FallbackFramework;

                Assert.Null(fallbackFrameworkNet45);
                Assert.Equal(2, fallbackFramework.Fallback.Count);
                Assert.Equal(NuGetFramework.Parse("portable-net45+win8"), fallbackFramework.Fallback[0]);
                Assert.Equal(NuGetFramework.Parse("dnxcore50"), fallbackFramework.Fallback[1]);

                // Verify original frameworks are trimmed
                Assert.Equal("net46", project1Spec.RestoreMetadata.OriginalTargetFrameworks[0]);
                Assert.Equal("netstandard16", project1Spec.RestoreMetadata.OriginalTargetFrameworks[1]);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyRuntimes()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "RuntimeIdentifiers", "win7-x86;linux-x64" },
                    { "RuntimeSupports", "net46.app;win8.app" },
                    { "CrossTargeting", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(2, project1Spec.RuntimeGraph.Runtimes.Count);
                Assert.Equal(2, project1Spec.RuntimeGraph.Supports.Count);
                Assert.Equal("win7-x86", project1Spec.RuntimeGraph.Runtimes["win7-x86"].RuntimeIdentifier);
                Assert.Equal("linux-x64", project1Spec.RuntimeGraph.Runtimes["linux-x64"].RuntimeIdentifier);
                Assert.Equal("net46.app", project1Spec.RuntimeGraph.Supports["net46.app"].Name);
                Assert.Equal("win8.app", project1Spec.RuntimeGraph.Supports["win8.app"].Name);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyRuntimes_Duplicates()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "RuntimeIdentifiers", "win7-x86;linux-x64;win7-x86;linux-x64" },
                    { "RuntimeSupports", "net46.app;win8.app;net46.app;win8.app" },
                    { "CrossTargeting", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(2, project1Spec.RuntimeGraph.Runtimes.Count);
                Assert.Equal(2, project1Spec.RuntimeGraph.Supports.Count);
                Assert.Equal("win7-x86", project1Spec.RuntimeGraph.Runtimes["win7-x86"].RuntimeIdentifier);
                Assert.Equal("linux-x64", project1Spec.RuntimeGraph.Runtimes["linux-x64"].RuntimeIdentifier);
                Assert.Equal("net46.app", project1Spec.RuntimeGraph.Supports["net46.app"].Name);
                Assert.Equal("win8.app", project1Spec.RuntimeGraph.Supports["win8.app"].Name);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCore_Conditionals()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project2Root = Path.Combine(workingDir, "b");

                var project1Path = Path.Combine(project1Root, "a.csproj");
                var project2Path = Path.Combine(project2Root, "b.csproj");

                var outputPath1 = Path.Combine(project1Root, "obj");
                var outputPath2 = Path.Combine(project2Root, "obj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard1.6" },
                    { "CrossTargeting", "true" },
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "b" },
                    { "OutputType", "netcore" },
                    { "OutputPath", outputPath2 },
                    { "ProjectUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project2Path },
                    { "TargetFrameworks", "net45;netstandard1.0" },
                    { "CrossTargeting", "true" },
                });

                // A -> B
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectReference" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectReferenceUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project2Path },
                    { "TargetFrameworks", "netstandard1.6" },
                    { "CrossTargeting", "true" },
                });

                // Package references
                // A net46 -> X
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "x" },
                    { "VersionRange", "1.0.0-beta.*" },
                    { "TargetFrameworks", "net46" },
                    { "CrossTargeting", "true" },
                });

                // A netstandard1.6 -> Z
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "z" },
                    { "VersionRange", "2.0.0" },
                    { "TargetFrameworks", "netstandard1.6" },
                    { "CrossTargeting", "true" },
                });

                // B ALL -> Y
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "y" },
                    { "VersionRange", "[1.0.0]" },
                    { "TargetFrameworks", "netstandard1.0;net45" },
                    { "CrossTargeting", "true" },
                });

                // Framework assembly
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "FrameworkAssembly" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "System.IO" },
                    { "TargetFrameworks", "net46" },
                    { "CrossTargeting", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single(e => e.Name == "a");
                var project2Spec = dgSpec.Projects.Single(e => e.Name == "b");

                var msbuildDependency = project1Spec.RestoreMetadata.TargetFrameworks
                    .Single(e => e.FrameworkName.Equals(NuGetFramework.Parse("netstandard1.6")))
                    .ProjectReferences
                    .Single();

                // Assert
                // Verify p2p reference
                Assert.Equal("AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A", msbuildDependency.ProjectUniqueName);
                Assert.Equal(project2Path, msbuildDependency.ProjectPath);
                Assert.Equal(LibraryIncludeFlags.All, msbuildDependency.IncludeAssets);
                Assert.Equal(LibraryIncludeFlags.None, msbuildDependency.ExcludeAssets);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, msbuildDependency.PrivateAssets);
                Assert.Equal("netstandard1.6", string.Join("|", project1Spec.RestoreMetadata.TargetFrameworks
                    .Where(e => e.ProjectReferences.Count > 0)
                    .Select(e => e.FrameworkName.GetShortFolderName())
                    .OrderBy(s => s, StringComparer.Ordinal)));

                // Dependency counts
                Assert.Equal(0, project1Spec.Dependencies.Count);
                Assert.Equal(0, project2Spec.Dependencies.Count);

                Assert.Equal(2, project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Count);
                Assert.Equal(1, project1Spec.GetTargetFramework(NuGetFramework.Parse("netstandard1.6")).Dependencies.Count);

                Assert.Equal(1, project2Spec.GetTargetFramework(NuGetFramework.Parse("net45")).Dependencies.Count);
                Assert.Equal(1, project2Spec.GetTargetFramework(NuGetFramework.Parse("netstandard1.0")).Dependencies.Count);

                // Verify dependencies
                var xDep = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Single(e => e.Name == "x");
                var zDep = project1Spec.GetTargetFramework(NuGetFramework.Parse("netstandard1.6")).Dependencies.Single(e => e.Name == "z");

                var yDep1 = project2Spec.GetTargetFramework(NuGetFramework.Parse("netstandard1.0")).Dependencies.Single();
                var yDep2 = project2Spec.GetTargetFramework(NuGetFramework.Parse("net45")).Dependencies.Single();

                var systemIO = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Single(e => e.Name == "System.IO");

                Assert.Equal("x", xDep.Name);
                Assert.Equal(VersionRange.Parse("1.0.0-beta.*"), xDep.LibraryRange.VersionRange);
                Assert.Equal(LibraryDependencyTarget.Package, xDep.LibraryRange.TypeConstraint);
                Assert.Equal(LibraryIncludeFlags.All, xDep.IncludeType);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, xDep.SuppressParent);

                Assert.Equal("z", zDep.Name);
                Assert.Equal(VersionRange.Parse("2.0.0"), zDep.LibraryRange.VersionRange);
                Assert.Equal(LibraryDependencyTarget.Package, zDep.LibraryRange.TypeConstraint);
                Assert.Equal(LibraryIncludeFlags.All, zDep.IncludeType);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, zDep.SuppressParent);

                Assert.Equal("y", yDep1.Name);
                Assert.Equal(VersionRange.Parse("[1.0.0]"), yDep1.LibraryRange.VersionRange);
                Assert.Equal(LibraryDependencyTarget.Package, yDep1.LibraryRange.TypeConstraint);
                Assert.Equal(LibraryIncludeFlags.All, yDep1.IncludeType);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, yDep1.SuppressParent);

                Assert.Equal(yDep1, yDep2);

                Assert.Equal("System.IO", systemIO.Name);
                Assert.Equal(VersionRange.All, systemIO.LibraryRange.VersionRange);
                Assert.Equal(LibraryDependencyTarget.Reference, systemIO.LibraryRange.TypeConstraint);
                Assert.Equal(LibraryIncludeFlags.All, systemIO.IncludeType);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, systemIO.SuppressParent);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_UAP_P2P()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project2Root = Path.Combine(workingDir, "b");

                var project1JsonPath = Path.Combine(project1Root, "project.json");
                var project2JsonPath = Path.Combine(project2Root, "project.json");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var project2Path = Path.Combine(project2Root, "b.csproj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectJsonPath", project1JsonPath },
                    { "ProjectName", "a" },
                    { "OutputType", "uap" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectJsonPath", project2JsonPath },
                    { "ProjectName", "b" },
                    { "OutputType", "uap" },
                    { "ProjectUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project2Path },
                });

                // A -> B
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectReference" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectReferenceUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project2Path },
                });

                var project1Json = @"
                {
                    ""version"": ""1.0.0"",
                    ""description"": """",
                    ""authors"": [ ""author"" ],
                    ""tags"": [ """" ],
                    ""projectUrl"": """",
                    ""licenseUrl"": """",
                    ""frameworks"": {
                        ""net45"": {
                        }
                    }
                }";

                var project2Json = @"
                {
                    ""version"": ""1.0.0"",
                    ""description"": """",
                    ""authors"": [ ""author"" ],
                    ""tags"": [ """" ],
                    ""projectUrl"": """",
                    ""licenseUrl"": """",
                    ""frameworks"": {
                        ""net45"": {
                        }
                    }
                }";

                Directory.CreateDirectory(project1Root);
                Directory.CreateDirectory(project2Root);

                File.WriteAllText(project1JsonPath, project1Json);
                File.WriteAllText(project2JsonPath, project2Json);

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single(e => e.Name == "a");
                var project2Spec = dgSpec.Projects.Single(e => e.Name == "b");

                var allDependencies1 = project1Spec.Dependencies.Concat(project1Spec.TargetFrameworks.Single().Dependencies).ToList();
                var allDependencies2 = project2Spec.Dependencies.Concat(project2Spec.TargetFrameworks.Single().Dependencies).ToList();
                var msbuildDependency = project1Spec.RestoreMetadata.TargetFrameworks.Single().ProjectReferences.Single();

                // Assert
                Assert.Equal("AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A", msbuildDependency.ProjectUniqueName);
                Assert.Equal(project2Path, msbuildDependency.ProjectPath);
                Assert.Equal(LibraryIncludeFlags.All, msbuildDependency.IncludeAssets);
                Assert.Equal(LibraryIncludeFlags.None, msbuildDependency.ExcludeAssets);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, msbuildDependency.PrivateAssets);
                Assert.Equal("net45", string.Join("|", project1Spec.RestoreMetadata.TargetFrameworks
                    .Select(e => e.FrameworkName.GetShortFolderName())
                    .OrderBy(s => s, StringComparer.Ordinal)));

                Assert.Equal(0, allDependencies2.Count);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_UAP_VerifyMetadata()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var projectJsonPath = Path.Combine(workingDir, "project.json");
                var projectPath = Path.Combine(workingDir, "a.csproj");

                var items = new List<IDictionary<string, string>>();
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectJsonPath", projectJsonPath },
                    { "ProjectName", "a" },
                    { "OutputType", "uap" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", projectPath },
                });

                var projectJson = @"
                {
                    ""version"": ""1.0.0"",
                    ""description"": """",
                    ""authors"": [ ""author"" ],
                    ""tags"": [ """" ],
                    ""projectUrl"": """",
                    ""licenseUrl"": """",
                    ""frameworks"": {
                        ""net45"": {
                        }
                    }
                }";

                File.WriteAllText(projectJsonPath, projectJson);

                // Act
                var spec = MSBuildRestoreUtility.GetPackageSpec(items.Select(CreateItems));

                // Assert
                Assert.Equal(projectJsonPath, spec.FilePath);
                Assert.Equal("a", spec.Name);
                Assert.Equal(ProjectStyle.ProjectJson, spec.RestoreMetadata.ProjectStyle);
                Assert.Equal("482C20DE-DFF9-4BD0-B90A-BD3201AA351A", spec.RestoreMetadata.ProjectUniqueName);
                Assert.Equal(projectPath, spec.RestoreMetadata.ProjectPath);
                Assert.Equal(0, spec.RestoreMetadata.TargetFrameworks.SelectMany(e => e.ProjectReferences).Count());
                Assert.Equal(projectJsonPath, spec.RestoreMetadata.ProjectJsonPath);
                Assert.Equal(NuGetFramework.Parse("net45"), spec.TargetFrameworks.Single().FrameworkName);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_UAP_IgnoresUnexpectedProperties()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var projectJsonPath = Path.Combine(workingDir, "project.json");
                var projectPath = Path.Combine(workingDir, "a.csproj");

                var items = new List<IDictionary<string, string>>();
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectJsonPath", projectJsonPath },
                    { "ProjectName", "a" },
                    { "OutputType", "uap" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", projectPath },
                    { "CrossTargeting", "true" },
                    { "RestoreLegacyPackagesDirectory", "true" },
                });

                var projectJson = @"
                {
                    ""version"": ""1.0.0"",
                    ""description"": """",
                    ""authors"": [ ""author"" ],
                    ""tags"": [ """" ],
                    ""projectUrl"": """",
                    ""licenseUrl"": """",
                    ""frameworks"": {
                        ""net45"": {
                        }
                    }
                }";

                File.WriteAllText(projectJsonPath, projectJson);

                // Act
                var spec = MSBuildRestoreUtility.GetPackageSpec(items.Select(CreateItems));

                // Assert
                Assert.False(spec.RestoreMetadata.CrossTargeting);
                Assert.False(spec.RestoreMetadata.LegacyPackagesDirectory);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NonNuGetProject()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var projectPath = Path.Combine(workingDir, "a.csproj");

                var items = new List<IDictionary<string, string>>();
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", projectPath },
                    { "TargetFrameworks", "net462" },
                    { "ProjectName", "a" },
                    { "CrossTargeting", "true" },
                });

                // Act
                var spec = MSBuildRestoreUtility.GetPackageSpec(items.Select(CreateItems));

                // Assert
                Assert.Equal(projectPath, spec.FilePath);
                Assert.Equal("a", spec.Name);
                Assert.Equal(ProjectStyle.Unknown, spec.RestoreMetadata.ProjectStyle);
                Assert.Equal("482C20DE-DFF9-4BD0-B90A-BD3201AA351A", spec.RestoreMetadata.ProjectUniqueName);
                Assert.Equal(projectPath, spec.RestoreMetadata.ProjectPath);
                Assert.Equal(NuGetFramework.Parse("net462"), spec.TargetFrameworks.Single().FrameworkName);
                Assert.Equal(0, spec.RestoreMetadata.TargetFrameworks.SelectMany(e => e.ProjectReferences).Count());
                Assert.Null(spec.RestoreMetadata.ProjectJsonPath);
            }
        }

        [Theory]
        [InlineData("a", "a")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData(null, "")]
        [InlineData(";;;;;;", "")]
        [InlineData("\n", "")]
        [InlineData(" ;\n;\t;;  \n ", "")]
        [InlineData("a;b;c", "a|b|c")]
        [InlineData(" a ; b ; c ", "a|b|c")]
        [InlineData("a;c \n ", "a|c")]
        public void MSBuildRestoreUtility_Split(string input, string expected)
        {
            // Arrange && Act
            var parts = StringUtility.Split(input);
            var output = string.Join("|", parts);

            // Assert
            Assert.Equal(expected, output);
        }

        [Theory]
        [InlineData("a", "a")]
        [InlineData(" ", null)]
        [InlineData(null, null)]
        [InlineData("\n", null)]
        [InlineData(" a ; b ; c ", "a ; b ; c")]
        [InlineData(" a;c\n ", "a;c")]
        public void MSBuildRestoreUtility_GetProperty_Trim(string input, string expected)
        {
            // Arrange
            var item = new MSBuildItem("a", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "key", input }
            });

            // Act
            var trimmed = item.GetProperty("key");
            var raw = item.GetProperty("key", trim: false);

            // Assert
            Assert.Equal(expected, trimmed);

            // Verify the value was not changed when it was stored
            Assert.Equal(input, raw);
        }

        private IMSBuildItem CreateItems(IDictionary<string, string> properties)
        {
            return new MSBuildItem(Guid.NewGuid().ToString(), properties);
        }
    }
}