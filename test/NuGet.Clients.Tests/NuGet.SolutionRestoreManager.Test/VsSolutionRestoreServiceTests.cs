// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.VisualStudio;
using Xunit;
using static NuGet.Frameworks.FrameworkConstants;

namespace NuGet.SolutionRestoreManager.Test
{
    public class VsSolutionRestoreServiceTests : IDisposable
    {
        private readonly TestDirectory _testDirectory;

        public VsSolutionRestoreServiceTests()
        {
            _testDirectory = TestDirectory.Create();
        }

        public void Dispose()
        {
            _testDirectory.Dispose();
        }

        [Fact]
        public void NominateProjectAsync_Always_SchedulesAutoRestore()
        {
            var cps = NewCpsProject();

            var cache = Mock.Of<IProjectSystemCache>();

            Mock.Get(cache)
                .Setup(x => x.AddProjectRestoreInfo(
                    It.IsAny<ProjectNames>(),
                    It.IsAny<DependencyGraphSpec>()))
                .Returns(true);

            var completedRestoreTask = Task.FromResult(true);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            Mock.Get(restoreWorker)
                .Setup(x => x.ScheduleRestoreAsync(
                    It.IsAny<SolutionRestoreRequest>(),
                    CancellationToken.None))
                .Returns(completedRestoreTask);

            var service = new VsSolutionRestoreService(
                cache, restoreWorker, NuGet.Common.NullLogger.Instance);

            // Act
            var actualRestoreTask = service.NominateProjectAsync(cps.ProjectFullPath, cps.ProjectRestoreInfo, CancellationToken.None);

            Assert.Same(completedRestoreTask, actualRestoreTask);

            Mock.Get(restoreWorker)
                .Verify(
                    x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), CancellationToken.None),
                    Times.Once(),
                    "Service should schedule auto-restore operation.");
        }

        [Fact]
        public async Task NominateProjectAsync_ConsoleAppTemplate()
        {
            var consoleAppProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp1.0"": {
            ""dependencies"": {
                ""Microsoft.NET.Sdk"": {
                    ""target"": ""Package"",
                    ""version"": ""1.0.0-alpha-20161019-1""
                },
                ""Microsoft.NETCore.App"": {
                    ""target"": ""Package"",
                    ""version"": ""1.0.1""
                }
            }
        }
    }
}";
            var projectName = "ConsoleApp1";
            var cps = NewCpsProject(consoleAppProjectJson, projectName);
            var pri = cps.ProjectRestoreInfo;
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal("1.0.0", actualProjectSpec.Version.ToString());

            var actualMetadata = actualProjectSpec.RestoreMetadata;
            Assert.NotNull(actualMetadata);
            Assert.Equal(projectFullPath, actualMetadata.ProjectPath);
            Assert.Equal(projectName, actualMetadata.ProjectName);
            Assert.Equal(ProjectStyle.PackageReference, actualMetadata.ProjectStyle);
            Assert.Equal(pri.BaseIntermediatePath, actualMetadata.OutputPath);

            Assert.Single(actualProjectSpec.TargetFrameworks);
            var actualTfi = actualProjectSpec.TargetFrameworks.Single();

            var expectedFramework = NuGetFramework.Parse("netcoreapp1.0");
            Assert.Equal(expectedFramework, actualTfi.FrameworkName);

            AssertPackages(actualTfi,
                "Microsoft.NET.Sdk:1.0.0-alpha-20161019-1",
                "Microsoft.NETCore.App:1.0.1");
        }

        [Fact]
        public async Task NominateProjectAsync_WithCliTool()
        {
            const string toolProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp1.0"": { }
    }
}";
            var cps = NewCpsProject(toolProjectJson);
            var pri = cps.Builder.WithTool("Foo.Test.Tools", "2.0.0").Build();
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);

            var actualToolSpec = actualRestoreSpec
                .Projects
                .Where(p => !object.ReferenceEquals(p, actualProjectSpec))
                .Single();
            var actualMetadata = actualToolSpec.RestoreMetadata;
            Assert.NotNull(actualMetadata);
            Assert.Equal(projectFullPath, actualMetadata.ProjectPath);
            Assert.Equal(ProjectStyle.DotnetCliTool, actualMetadata.ProjectStyle);
            Assert.Null(actualMetadata.OutputPath);

            var actualToolFramework = actualToolSpec
                .TargetFrameworks
                .Single()
                .FrameworkName;
            Assert.Equal(CommonFrameworks.NetCoreApp10, actualToolFramework);

            var actualToolLibrary = actualToolSpec
                .TargetFrameworks
                .Single()
                .Dependencies
                .Single();
            Assert.Equal("Foo.Test.Tools", actualToolLibrary.Name);
            Assert.Equal("2.0.0", actualToolLibrary.LibraryRange.VersionRange.OriginalString);
        }

        [Theory]
        [InlineData("netcoreapp2.0", @"..\packages", @"..\source1;..\source2", @"..\fallback1;..\fallback2")]
        [InlineData("netcoreapp2.0", @"C:\packagesPath", @"..\source1;..\source2", @"C:\fallback1;C:\fallback2")]
        [InlineData("netcoreapp2.0", null, null, null)]
        [InlineData("netcoreapp1.0", null, null, null)]
        public async Task NominateProjectAsync_WithCliTool_RestoreSettings(
            string toolFramework, string restorePackagesPath, string restoreSources, string fallbackFolders)
        {
            var expectedToolFramework = NuGetFramework.Parse(toolFramework);

            var cps = NewCpsProject(@"{ }");
            var pri = cps.Builder
                .WithTool("Foo.Test.Tools", "2.0.0")
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestorePackagesPath", restorePackagesPath),
                                new VsProjectProperty("RestoreSources", restoreSources),
                                new VsProjectProperty("RestoreFallbackFolders", fallbackFolders),
                                new VsProjectProperty("DotnetCliToolTargetFramework", toolFramework) }))
                .Build();
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);

            var actualToolSpec = actualRestoreSpec
                .Projects
                .Where(p => !object.ReferenceEquals(p, actualProjectSpec))
                .Single();
            var actualMetadata = actualToolSpec.RestoreMetadata;
            Assert.NotNull(actualMetadata);
            Assert.Equal(projectFullPath, actualMetadata.ProjectPath);
            Assert.Equal(ProjectStyle.DotnetCliTool, actualMetadata.ProjectStyle);
            Assert.Null(actualMetadata.OutputPath);

            var actualToolFramework = actualToolSpec
                .TargetFrameworks
                .Single()
                .FrameworkName;
            Assert.Equal(expectedToolFramework, actualToolFramework);

            var actualToolLibrary = actualToolSpec
                .TargetFrameworks
                .Single()
                .Dependencies
                .Single();
            Assert.Equal("Foo.Test.Tools", actualToolLibrary.Name);
            Assert.Equal("2.0.0", actualToolLibrary.LibraryRange.VersionRange.OriginalString);

            Assert.Equal(restorePackagesPath, actualToolSpec.RestoreMetadata.PackagesPath);

            var specSources = actualToolSpec.RestoreMetadata.Sources?.Select(e => e.Source);
            var expectedSources = MSBuildStringUtility.Split(restoreSources);
            Assert.True(Enumerable.SequenceEqual(expectedSources.OrderBy(t => t), specSources.OrderBy(t => t)));

            var specFallback = actualToolSpec.RestoreMetadata.FallbackFolders;
            var expectedFallback = MSBuildStringUtility.Split(fallbackFolders);
            Assert.True(Enumerable.SequenceEqual(expectedFallback.OrderBy(t => t), specFallback.OrderBy(t => t)));
        }

        [Theory]
        [InlineData(
@"{
    ""frameworks"": {
        ""netstandard1.4"": { }
    }
}", "netstandard1.4", "netstandard1.4")]
        [InlineData(
@"{
    ""frameworks"": {
        ""netstandard1.4"": { },
        ""net46"": { }
    }
}", "netstandard1.4;net46", "netstandard1.4;net46")]
        [InlineData(
@"{
    ""frameworks"": {
        ""netstandard1.4"": { },
        ""net46"": { }
    }
}", "\r\n    netstandard1.4;\r\n    net46\r\n    ", "netstandard1.4;net46")]
        public async Task NominateProjectAsync_CrossTargeting(
            string projectJson, string rawOriginalTargetFrameworks, string expectedOriginalTargetFrameworks)
        {
            var cps = NewCpsProject(
                projectJson: projectJson,
                crossTargeting: true);
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.ProjectRestoreInfo;
            pri.OriginalTargetFrameworks = rawOriginalTargetFrameworks;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);

            var actualMetadata = actualProjectSpec.RestoreMetadata;
            Assert.NotNull(actualMetadata);
            Assert.True(actualMetadata.CrossTargeting);

            var actualOriginalTargetFrameworks = string.Join(";", actualMetadata.OriginalTargetFrameworks);
            Assert.Equal(
                expectedOriginalTargetFrameworks,
                actualOriginalTargetFrameworks);
        }

        [Fact]
        public async Task NominateProjectAsync_Imports()
        {
            const string projectJson = @"{
    ""frameworks"": {
        ""netstandard1.4"": {
            ""imports"": [""dotnet5.3"",""portable-net452+win81""]
        }
    }
}";
            var cps = NewCpsProject(projectJson);
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);

            var actualTfi = actualProjectSpec.TargetFrameworks.Single();
            var actualImports = string.Join(";", actualTfi.Imports.Select(x => x.GetShortFolderName()));
            Assert.Equal("dotnet5.3;portable-net452+win81", actualImports);
        }

        [Fact]
        public async Task NominateProjectAsync_WithValidPackageVersion()
        {
            const string projectJson = @"{
    ""version"": ""1.2.0-beta1"",
    ""frameworks"": {
        ""netcoreapp1.0"": { }
    }
}";
            var cps = NewCpsProject(projectJson);
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal("1.2.0-beta1", actualProjectSpec.Version.ToString());
        }

        [Theory]
        [InlineData("1.2.3", "1.2.3")]
        [InlineData("1.2.0-beta1", "1.2.0-beta1")]
        [InlineData("1.0.0", "1.0.0.0")]
        public async Task NominateProjectAsync_WithIdenticalPackageVersions(string version1, string version2)
        {
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageVersion", version1) }))
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "net46",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageVersion", version2) }))
                .Build();

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal(version1, actualProjectSpec.Version.ToString());
        }

        // The data in the restore settings should be unprocessed meaning the paths should stay relative.
        // The processing of the paths will be done later in the netcorepackagereferenceproject
        [Theory]
        [InlineData(@"..\packages", @"..\source1;..\source2", @"..\fallback1;..\fallback2")]
        [InlineData(@"C:\packagesPath", @"..\source1;..\source2", @"C:\fallback1;C:\fallback2")]
        [InlineData(null, null, null)]
        public async Task NominateProjectAsync_RestoreSettings(string restorePackagesPath, string restoreSources, string fallbackFolders)
        {
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {new VsProjectProperty("RestorePackagesPath", restorePackagesPath),
                               new VsProjectProperty("RestoreSources", restoreSources),
                               new VsProjectProperty("RestoreFallbackFolders", fallbackFolders)}))
                .Build();

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal(restorePackagesPath, actualProjectSpec.RestoreMetadata.PackagesPath);

            var specSources = actualProjectSpec.RestoreMetadata.Sources?.Select(e => e.Source);
            var expectedSources = MSBuildStringUtility.Split(restoreSources);
            Assert.True(Enumerable.SequenceEqual(expectedSources.OrderBy(t => t), specSources.OrderBy(t => t)));

            var specFallback = actualProjectSpec.RestoreMetadata.FallbackFolders;
            var expectedFallback = MSBuildStringUtility.Split(fallbackFolders);
            Assert.True(Enumerable.SequenceEqual(expectedFallback.OrderBy(t => t), specFallback.OrderBy(t => t)));
        }

        [Theory]
        [InlineData(@"..\packages", @"..\source1;..\source2", @"..\fallback1;Clear;..\fallback2")]
        [InlineData(@"C:\packagesPath", @"Clear;..\source1;..\source2", @"C:\fallback1;C:\fallback2")]
        public async Task NominateProjectAsync_RestoreSettingsClear(string restorePackagesPath, string restoreSources, string fallbackFolders)
        {
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {new VsProjectProperty("RestorePackagesPath", restorePackagesPath),
                               new VsProjectProperty("RestoreSources", restoreSources),
                               new VsProjectProperty("RestoreFallbackFolders", fallbackFolders)}))
                .Build();

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal(restorePackagesPath, actualProjectSpec.RestoreMetadata.PackagesPath);

            var specSources = actualProjectSpec.RestoreMetadata.Sources?.Select(e => e.Source);
            var expectedSources = MSBuildStringUtility.Split(restoreSources).Any(e => StringComparer.OrdinalIgnoreCase.Equals("clear", e)) ? new string[] { "Clear" } : MSBuildStringUtility.Split(restoreSources);
            Assert.True(Enumerable.SequenceEqual(expectedSources.OrderBy(t => t), specSources.OrderBy(t => t)));

            var specFallback = actualProjectSpec.RestoreMetadata.FallbackFolders;
            var expectedFallback = MSBuildStringUtility.Split(fallbackFolders).Any(e => StringComparer.OrdinalIgnoreCase.Equals("clear", e)) ? new string[] { "Clear" } : MSBuildStringUtility.Split(fallbackFolders);
            Assert.True(Enumerable.SequenceEqual(expectedFallback.OrderBy(t => t), specFallback.OrderBy(t => t)));
        }

        [Fact]
        public async Task NominateProjectAsync_CacheFilePathInPackageSpec_Succeeds()
        {
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new IVsProjectProperty[] {}))
                .Build();

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal(Path.Combine(actualProjectSpec.RestoreMetadata.OutputPath,$"{Path.GetFileName(projectFullPath)}.nuget.cache"), actualProjectSpec.RestoreMetadata.CacheFilePath);
        }

        [Theory]
        [InlineData("1.0.0", "1.2.3")]
        [InlineData("1.0.0", "")]
        [InlineData("1.0.0", "   ")]
        [InlineData("1.0.0", null)]
        public async Task NominateProjectAsync_WithDifferentPackageVersions_Fails(string version1, string version2)
        {
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageVersion", version1) }))
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "net46",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageVersion", version2) }))
                .Build();

            var cache = Mock.Of<IProjectSystemCache>();
            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();

            var service = new VsSolutionRestoreService(
                cache, restoreWorker, NuGet.Common.NullLogger.Instance);

            // Act
            var result = await service.NominateProjectAsync(projectFullPath, pri, CancellationToken.None);

            Assert.False(result, "Project restore nomination must fail.");
        }

        [Fact]
        public async Task NominateProjectAsync_PackageId()
        {
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageId", "TestPackage") }))
                .Build();

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal("TestPackage", actualProjectSpec.Name);
        }

        [Fact]
        public async Task NominateProjectAsync_VerifySourcesAreCombinedAcrossFrameworks()
        {
            var cps = NewCpsProject(@"{ }");
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectSources", "a;d"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", "x;z")}))
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectSources", "b"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", "y")}))
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp2.1",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectSources", "b"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", "y")}))
                .Build();
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);
            var metadata = actualRestoreSpec.Projects.Single().RestoreMetadata;

            // Assert
            metadata.Sources.Select(e => e.Source).ShouldBeEquivalentTo(new[] { "base", VSRestoreSettingsUtilities.AdditionalValue, "a", "b", "d" });
            metadata.FallbackFolders.ShouldBeEquivalentTo(new[] { "base", VSRestoreSettingsUtilities.AdditionalValue, "x", "y", "z" });
        }

        [Fact]
        public async Task NominateProjectAsync_VerifyExcludedFallbackFoldersAreRemoved()
        {
            var cps = NewCpsProject(@"{ }");
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", "x")})) // Add FF
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectSources", "a"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFoldersExcludes", "x")})) // Remove FF
                .Build();
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);
            var metadata = actualRestoreSpec.Projects.Single().RestoreMetadata;

            // Assert
            metadata.Sources.Select(e => e.Source).ShouldBeEquivalentTo(new[] { "base", VSRestoreSettingsUtilities.AdditionalValue, "a" });
            metadata.FallbackFolders.ShouldBeEquivalentTo(new[] { "base" });
        }

        [Fact]
        public async Task NominateProjectAsync_VerifyToolRestoresUseAdditionalSourcesAndFallbackFolders()
        {
            var cps = NewCpsProject(@"{ }");
            var pri = cps.Builder
                .WithTool("ToolTest", "2.0.0")
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", "x;y")}))
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectSources", "a"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFoldersExcludes", "y")}))
                .Build();
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Find the tool spec
            var metadata = actualRestoreSpec.Projects.Single(e => e.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool).RestoreMetadata;

            // Assert
            metadata.Sources.Select(e => e.Source).ShouldBeEquivalentTo(new[] { "base", VSRestoreSettingsUtilities.AdditionalValue, "a" });
            metadata.FallbackFolders.ShouldBeEquivalentTo(new[] { "base", VSRestoreSettingsUtilities.AdditionalValue, "x" });
        }

        [Theory]
        [InlineData(@"..\source1", @"..\additionalsource", @"..\fallback1", @"..\additionalFallback1")]
        [InlineData(@"..\source1", null, @"..\fallback1", null)]
        [InlineData(null, @"..\additionalsource", null, @"..\additionalFallback1")]
        [InlineData(@"Clear;C:\source1", @"C:\additionalsource", @"C:\fallback1;Clear", @"C:\additionalFallback1")]
        [InlineData(@"C:\source1;Clear", @"C:\additionalsource", @"Clear;C:\fallback1", @"C:\additionalFallback1")]
        public async Task NominateProjectAsync_WithRestoreAdditionalSourcesAndFallbackFolders(string restoreSources, string restoreAdditionalProjectSources, string restoreFallbackFolders, string restoreAdditionalFallbackFolders)
        {
            var cps = NewCpsProject(@"{ }");
            var pri = cps.Builder
                .WithTool("Foo.Test", "2.0.0")
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", restoreSources),
                                new VsProjectProperty("RestoreFallbackFolders", restoreFallbackFolders),
                                new VsProjectProperty("RestoreAdditionalProjectSources", restoreAdditionalProjectSources),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", restoreAdditionalFallbackFolders)}))
                .Build();
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);

           var specSources = actualProjectSpec.RestoreMetadata.Sources?.Select(e => e.Source);

            var expectedSources = 
                (MSBuildStringUtility.Split(restoreSources).Any(e => StringComparer.OrdinalIgnoreCase.Equals("clear", e)) ?
                    new string[] { "Clear" } :
                    MSBuildStringUtility.Split(restoreSources)).
                Concat(
                restoreAdditionalProjectSources != null ?
                    new List<string>() { VSRestoreSettingsUtilities.AdditionalValue }.Concat(MSBuildStringUtility.Split(restoreAdditionalProjectSources)):
                    new string[] { }
                );

            Assert.True(Enumerable.SequenceEqual(expectedSources.OrderBy(t => t), specSources.OrderBy(t => t)));

            var specFallback = actualProjectSpec.RestoreMetadata.FallbackFolders;

            var expectedFallback =
                (MSBuildStringUtility.Split(restoreFallbackFolders).Any(e => StringComparer.OrdinalIgnoreCase.Equals("clear", e)) ?
                    new string[] { "Clear" } :
                    MSBuildStringUtility.Split(restoreFallbackFolders)).
                Concat(
                restoreAdditionalFallbackFolders != null ?
                    new List<string>() { VSRestoreSettingsUtilities.AdditionalValue }.Concat(MSBuildStringUtility.Split(restoreAdditionalFallbackFolders)) :
                    new string[] { }
                );

            Assert.True(
                Enumerable.SequenceEqual(expectedFallback.OrderBy(t => t), specFallback.OrderBy(t => t)),
                "expected: " + string.Join(",", expectedFallback.ToArray()) + "\nactual: " + string.Join(",", specFallback.ToArray()));
        }


        [Theory]
        [InlineData(@"C:\source1", @"C:\additionalsource",@"C:\source1;C:\additionalsource", @"C:\fallback1", @"C:\additionalFallback1", @"C:\fallback1;C:\additionalFallback1")]
        [InlineData(@"C:\source1", null, @"C:\source1", @"C:\fallback1", null, @"C:\fallback1")]
        [InlineData(null, @"C:\additionalsource", @"C:\additionalsource",  null, @"C:\additionalFallback1", @"C:\additionalFallback1")]
        [InlineData(@"Clear;C:\source1", @"C:\additionalsource", @"C:\additionalsource",  @"C:\fallback1;Clear", @"C:\additionalFallback1", @"C:\additionalFallback1")]
        [InlineData(@"C:\source1;Clear", @"C:\additionalsource", @"C:\additionalsource", @"Clear;C:\fallback1", @"C:\additionalFallback1", @"C:\additionalFallback1")]
        public async Task VSSolutionRestoreService_VSRestoreSettingsUtilities_Integration(string restoreSources, string restoreAdditionalProjectSources, string expectedRestoreSources, string restoreFallbackFolders, string restoreAdditionalFallbackFolders, string expectedFallbackFolders)
        {
            var cps = NewCpsProject(@"{ }");
            var pri = cps.Builder
                .WithTool("Foo.Test", "2.0.0")
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", restoreSources),
                                new VsProjectProperty("RestoreFallbackFolders", restoreFallbackFolders),
                                new VsProjectProperty("RestoreAdditionalProjectSources", restoreAdditionalProjectSources),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", restoreAdditionalFallbackFolders)}))
                .Build();
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);

           var specSources = VSRestoreSettingsUtilities.GetSources(NullSettings.Instance, actualProjectSpec).Select(e => e.Source);

            var expectedSources = MSBuildStringUtility.Split(expectedRestoreSources);

            Assert.True(Enumerable.SequenceEqual(expectedSources.OrderBy(t => t), specSources.OrderBy(t => t)));

            var specFallback = VSRestoreSettingsUtilities.GetFallbackFolders(NullSettings.Instance, actualProjectSpec);

            var expectedFallback = MSBuildStringUtility.Split(expectedFallbackFolders);

            Assert.True(
                Enumerable.SequenceEqual(expectedFallback.OrderBy(t => t), specFallback.OrderBy(t => t)),
                "expected: " + string.Join(",", expectedFallback.ToArray()) + "\nactual: " + string.Join(",", specFallback.ToArray()));
        }

        [Theory]
        [InlineData("true", null, "false")]
        [InlineData(null, "packages.A.lock.json", null)]
        [InlineData("true", null, "true")]
        [InlineData("false", null, "false")]
        public async Task NominateProjectAsync_LockFileSettings(
            string restorePackagesWithLockFile,
            string lockFilePath,
            string restoreLockedMode)
        {
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {
                            new VsProjectProperty("RestorePackagesWithLockFile", restorePackagesWithLockFile),
                            new VsProjectProperty("NuGetLockFilePath", lockFilePath),
                            new VsProjectProperty("RestoreLockedMode", restoreLockedMode)}))
                .Build();

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal(restorePackagesWithLockFile, actualProjectSpec.RestoreMetadata.RestoreLockProperties.RestorePackagesWithLockFile);
            Assert.Equal(lockFilePath, actualProjectSpec.RestoreMetadata.RestoreLockProperties.NuGetLockFilePath);
            Assert.Equal(MSBuildStringUtility.IsTrue(restoreLockedMode), actualProjectSpec.RestoreMetadata.RestoreLockProperties.RestoreLockedMode);
        }

        private async Task<DependencyGraphSpec> CaptureNominateResultAsync(
            string projectFullPath, IVsProjectRestoreInfo pri)
        {
            DependencyGraphSpec capturedRestoreSpec = null;

            var cache = Mock.Of<IProjectSystemCache>();
            Mock.Get(cache)
                .Setup(x => x.AddProjectRestoreInfo(
                    It.IsAny<ProjectNames>(),
                    It.IsAny<DependencyGraphSpec>()))
                .Callback<ProjectNames, DependencyGraphSpec>(
                    (_, dg) => { capturedRestoreSpec = dg; })
                .Returns(true);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            Mock.Get(restoreWorker)
                .Setup(x => x.ScheduleRestoreAsync(
                    It.IsAny<SolutionRestoreRequest>(),
                    CancellationToken.None))
                .ReturnsAsync(true);

            var service = new VsSolutionRestoreService(
                cache, restoreWorker, NuGet.Common.NullLogger.Instance);

            // Act
            var result = await service.NominateProjectAsync(projectFullPath, pri, CancellationToken.None);

            Assert.True(result, "Project restore nomination should succeed.");

            return capturedRestoreSpec;
        }

        private TestContext NewCpsProject(
            string projectJson = null,
            string projectName = null,
            bool crossTargeting = false)
        {
            const string DefaultProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp1.0"": {
            ""dependencies"": { }
        }
    }
}";
            if (projectName == null)
            {
                projectName = $"{Guid.NewGuid()}";
            }

            var projectLocation = _testDirectory.Path;
            var projectFullPath = Path.Combine(projectLocation, $"{projectName}.csproj");
            var baseIntermediatePath = Path.Combine(projectLocation, "obj");
            Directory.CreateDirectory(baseIntermediatePath);

            var spec = JsonPackageSpecReader.GetPackageSpec(projectJson ?? DefaultProjectJson, projectName, projectFullPath);
            var builder = ProjectRestoreInfoBuilder.FromPackageSpec(
                spec,
                baseIntermediatePath,
                crossTargeting);

            return new TestContext
            {
                ProjectFullPath = projectFullPath,
                Builder = builder
            };
        }

        private static void AssertPackages(TargetFrameworkInformation actualTfi, params string[] expectedPackages)
        {
            var actualPackages = actualTfi
                .Dependencies
                .Where(ld => ld.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package)
                .Select(ld => $"{ld.Name}:{ld.LibraryRange.VersionRange.OriginalString}");

            Assert.Equal(expectedPackages, actualPackages);
        }

        private class TestContext
        {
            public string ProjectFullPath { get; set; }
            public ProjectRestoreInfoBuilder Builder { get; set; }

            public VsProjectRestoreInfo ProjectRestoreInfo => Builder.Build();
        }
    }
}
