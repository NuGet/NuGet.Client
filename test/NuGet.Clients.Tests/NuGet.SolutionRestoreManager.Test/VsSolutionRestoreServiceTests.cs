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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NominateProjectAsync_Always_SchedulesAutoRestore(bool isV2Nomination)
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
                cache, restoreWorker, NullLogger.Instance);

            // Act
            var actualRestoreTask = isV2Nomination ? service.NominateProjectAsync(cps.ProjectFullPath, cps.ProjectRestoreInfo2, CancellationToken.None)
                : service.NominateProjectAsync(cps.ProjectFullPath, cps.ProjectRestoreInfo, CancellationToken.None);

            Assert.Same(completedRestoreTask, actualRestoreTask);

            Mock.Get(restoreWorker)
                .Verify(
                    x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), CancellationToken.None),
                    Times.Once(),
                    "Service should schedule auto-restore operation.");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NominateProjectAsync_ConsoleAppTemplate(bool isV2Nomination)
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
            var projectFullPath = cps.ProjectFullPath;
            var expectedBaseIntermediate = cps.ProjectRestoreInfo.BaseIntermediatePath == cps.ProjectRestoreInfo2.BaseIntermediatePath ? cps.ProjectRestoreInfo.BaseIntermediatePath : "The test builder is broken!";

            // Act
            var actualRestoreSpec = isV2Nomination ?
                await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

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
            Assert.Equal(expectedBaseIntermediate, actualMetadata.OutputPath);

            Assert.Single(actualProjectSpec.TargetFrameworks);
            var actualTfi = actualProjectSpec.TargetFrameworks.Single();

            var expectedFramework = NuGetFramework.Parse("netcoreapp1.0");
            Assert.Equal(expectedFramework, actualTfi.FrameworkName);

            AssertPackages(actualTfi,
                "Microsoft.NET.Sdk:1.0.0-alpha-20161019-1",
                "Microsoft.NETCore.App:1.0.1");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NominateProjectAsync_WithCliTool(bool isV2Nomination)
        {
            const string toolProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp1.0"": { }
    }
}";
            var cps = NewCpsProject(toolProjectJson);
            var builder = cps.Builder.WithTool("Foo.Test.Tools", "2.0.0");
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = isV2Nomination ?
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);


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
        [InlineData("netcoreapp2.0", @"..\packages", @"..\source1;..\source2", @"..\fallback1;..\fallback2", false)]
        [InlineData("netcoreapp2.0", @"C:\packagesPath", @"..\source1;..\source2", @"C:\fallback1;C:\fallback2", false)]
        [InlineData("netcoreapp2.0", null, null, null, false)]
        [InlineData("netcoreapp1.0", null, null, null, false)]
        [InlineData("netcoreapp2.0", @"..\packages", @"..\source1;..\source2", @"..\fallback1;..\fallback2")]
        [InlineData("netcoreapp2.0", @"C:\packagesPath", @"..\source1;..\source2", @"C:\fallback1;C:\fallback2")]
        [InlineData("netcoreapp2.0", null, null, null)]
        [InlineData("netcoreapp1.0", null, null, null)]
        public async Task NominateProjectAsync_WithCliTool_RestoreSettings(
            string toolFramework, string restorePackagesPath, string restoreSources, string fallbackFolders, bool isV2Nominate = true)
        {
            var expectedToolFramework = NuGetFramework.Parse(toolFramework);

            var tfms = isV2Nominate ?
                (IVsTargetFrameworkInfo)
                new VsTargetFrameworkInfo2(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestorePackagesPath", restorePackagesPath),
                                new VsProjectProperty("RestoreSources", restoreSources),
                                new VsProjectProperty("RestoreFallbackFolders", fallbackFolders),
                                new VsProjectProperty("DotnetCliToolTargetFramework", toolFramework) }) :
                new VsTargetFrameworkInfo(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestorePackagesPath", restorePackagesPath),
                                new VsProjectProperty("RestoreSources", restoreSources),
                                new VsProjectProperty("RestoreFallbackFolders", fallbackFolders),
                                new VsProjectProperty("DotnetCliToolTargetFramework", toolFramework) });
            var cps = NewCpsProject(@"{ }");
            var builder = cps.Builder
                .WithTool("Foo.Test.Tools", "2.0.0")
                .WithTargetFrameworkInfo(tfms);

            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = isV2Nominate ?
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);

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
        [InlineData(
@"{
    ""frameworks"": {
        ""netstandard1.4"": { }
    }
}", "netstandard1.4", "netstandard1.4", false)]
        [InlineData(
@"{
    ""frameworks"": {
        ""netstandard1.4"": { },
        ""net46"": { }
    }
}", "netstandard1.4;net46", "netstandard1.4;net46", false)]
        [InlineData(
@"{
    ""frameworks"": {
        ""netstandard1.4"": { },
        ""net46"": { }
    }
}", "\r\n    netstandard1.4;\r\n    net46\r\n    ", "netstandard1.4;net46", false)]
        public async Task NominateProjectAsync_CrossTargeting(
            string projectJson, string rawOriginalTargetFrameworks, string expectedOriginalTargetFrameworks, bool isV2Nominate = true)
        {
            var cps = NewCpsProject(
                projectJson: projectJson,
                crossTargeting: true);
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.ProjectRestoreInfo;
            var pri2 = cps.ProjectRestoreInfo2;

            pri.OriginalTargetFrameworks = rawOriginalTargetFrameworks;
            pri2.OriginalTargetFrameworks = rawOriginalTargetFrameworks;

            // Act
            var actualRestoreSpec = isV2Nominate ?
                await CaptureNominateResultAsync(projectFullPath, pri) :
                await CaptureNominateResultAsync(projectFullPath, pri2);

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NominateProjectAsync_Imports(bool isV2Nomination)
        {
            const string projectJson = @"{
    ""frameworks"": {
        ""netstandard1.4"": {
            ""imports"": [""dotnet5.3"",""portable-net452+win81""]
        }
    },
}";
            var cps = NewCpsProject(projectJson);
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = isV2Nomination ?
                await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);
            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);

            var actualTfi = actualProjectSpec.TargetFrameworks.Single();
            var actualImports = string.Join(";", actualTfi.Imports.Select(x => x.GetShortFolderName()));
            Assert.Equal("dotnet5.3;portable-net452+win81", actualImports);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NominateProjectAsync_WithValidPackageVersion(bool isV2Nomination)
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
            var actualRestoreSpec = isV2Nomination ?
                await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

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
                .ProjectRestoreInfo;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal(version1, actualProjectSpec.Version.ToString());
        }

        [Theory]
        [InlineData("1.2.3", "1.2.3")]
        [InlineData("1.2.0-beta1", "1.2.0-beta1")]
        [InlineData("1.0.0", "1.0.0.0")]
        public async Task NominateProjectAsync_PRI2_WithIdenticalPackageVersions(string version1, string version2)
        {
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageVersion", version1) }))
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "net46",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageVersion", version2) }))
                .ProjectRestoreInfo2;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal(version1, actualProjectSpec.Version.ToString());
        }

        // The data in the restore settings should be unprocessed meaning the paths should stay relative.
        // The processing of the paths will be done later in the netcorepackagereferenceproject
        [Theory]
        [InlineData(@"..\packages", @"..\source1;..\source2", @"..\fallback1;..\fallback2", false)]
        [InlineData(@"C:\packagesPath", @"..\source1;..\source2", @"C:\fallback1;C:\fallback2", false)]
        [InlineData(null, null, null, false)]
        [InlineData(@"..\packages", @"..\source1;..\source2", @"..\fallback1;..\fallback2", true)]
        [InlineData(@"C:\packagesPath", @"..\source1;..\source2", @"C:\fallback1;C:\fallback2", true)]
        [InlineData(null, null, null, true)]
        public async Task NominateProjectAsync_RestoreSettings(string restorePackagesPath, string restoreSources, string fallbackFolders, bool isV2Nominate)
        {
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;

            var vstfm = isV2Nominate ?
                (IVsTargetFrameworkInfo) new VsTargetFrameworkInfo2(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {new VsProjectProperty("RestorePackagesPath", restorePackagesPath),
                               new VsProjectProperty("RestoreSources", restoreSources),
                               new VsProjectProperty("RestoreFallbackFolders", fallbackFolders)}) :

                new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {new VsProjectProperty("RestorePackagesPath", restorePackagesPath),
                               new VsProjectProperty("RestoreSources", restoreSources),
                               new VsProjectProperty("RestoreFallbackFolders", fallbackFolders)});

            var builder = cps.Builder
                .WithTargetFrameworkInfo(vstfm);

            // Act
            var actualRestoreSpec = isV2Nominate ?
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);

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
        [InlineData(@"..\packages", @"..\source1;..\source2", @"..\fallback1;Clear;..\fallback2", false)]
        [InlineData(@"C:\packagesPath", @"Clear;..\source1;..\source2", @"C:\fallback1;C:\fallback2", false)]
        [InlineData(@"..\packages", @"..\source1;..\source2", @"..\fallback1;Clear;..\fallback2", true)]
        [InlineData(@"C:\packagesPath", @"Clear;..\source1;..\source2", @"C:\fallback1;C:\fallback2", true)]
        public async Task NominateProjectAsync_RestoreSettingsClear(string restorePackagesPath, string restoreSources, string fallbackFolders, bool isV2Nominate)
        {
            var vstfm = isV2Nominate ?
                (IVsTargetFrameworkInfo)
                new VsTargetFrameworkInfo2(
                    "netcoreapp1.0",
                    Enumerable.Empty<IVsReferenceItem>(),
                    Enumerable.Empty<IVsReferenceItem>(),
                    Enumerable.Empty<IVsReferenceItem>(),
                    Enumerable.Empty<IVsReferenceItem>(),
                    new[] {new VsProjectProperty("RestorePackagesPath", restorePackagesPath),
                            new VsProjectProperty("RestoreSources", restoreSources),
                            new VsProjectProperty("RestoreFallbackFolders", fallbackFolders)}) :
                new VsTargetFrameworkInfo(
                    "netcoreapp1.0",
                    Enumerable.Empty<IVsReferenceItem>(),
                    Enumerable.Empty<IVsReferenceItem>(),
                    new[] {new VsProjectProperty("RestorePackagesPath", restorePackagesPath),
                            new VsProjectProperty("RestoreSources", restoreSources),
                            new VsProjectProperty("RestoreFallbackFolders", fallbackFolders)});

            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var builder = cps.Builder
                .WithTargetFrameworkInfo(
                    vstfm);

            // Act
            var actualRestoreSpec = isV2Nominate ?
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NominateProjectAsync_CacheFilePathInPackageSpec_Succeeds(bool isV2Nominate)
        {

            var vstfm = isV2Nominate ?
                        (IVsTargetFrameworkInfo)
                        new VsTargetFrameworkInfo2(
                            "netcoreapp1.0",
                            Enumerable.Empty<IVsReferenceItem>(),
                            Enumerable.Empty<IVsReferenceItem>(),
                            Enumerable.Empty<IVsReferenceItem>(),
                            Enumerable.Empty<IVsReferenceItem>(),
                            new IVsProjectProperty[] { }) :
                        new VsTargetFrameworkInfo(
                            "netcoreapp1.0",
                            Enumerable.Empty<IVsReferenceItem>(),
                            Enumerable.Empty<IVsReferenceItem>(),
                            new IVsProjectProperty[] { });

            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var builder = cps.Builder
                .WithTargetFrameworkInfo(vstfm);

            // Act
           var actualRestoreSpec = isV2Nominate ?
                        await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                        await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);

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
                .ProjectRestoreInfo;

            var cache = Mock.Of<IProjectSystemCache>();
            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();

            var service = new VsSolutionRestoreService(
                cache, restoreWorker, NuGet.Common.NullLogger.Instance);

            // Act
            var result = await service.NominateProjectAsync(projectFullPath, pri, CancellationToken.None);

            Assert.False(result, "Project restore nomination must fail.");
        }

        [Theory]
        [InlineData("1.0.0", "1.2.3")]
        [InlineData("1.0.0", "")]
        [InlineData("1.0.0", "   ")]
        [InlineData("1.0.0", null)]
        public async Task NominateProjectAsync_PRI2_WithDifferentPackageVersions_Fails(string version1, string version2)
        {
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageVersion", version1) }))
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "net46",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageVersion", version2) }))
                .ProjectRestoreInfo2;

            var cache = Mock.Of<IProjectSystemCache>();
            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();

            var service = new VsSolutionRestoreService(
                cache, restoreWorker, NuGet.Common.NullLogger.Instance);

            // Act
            var result = await service.NominateProjectAsync(projectFullPath, pri, CancellationToken.None);

            Assert.False(result, "Project restore nomination must fail.");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NominateProjectAsync_PackageId(bool isV2Nominate)
        {
            var vstfm = isV2Nominate ?
                (IVsTargetFrameworkInfo) new VsTargetFrameworkInfo2(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageId", "TestPackage") }) :
                new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("PackageId", "TestPackage") });

            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var builder = cps.Builder
                .WithTargetFrameworkInfo(vstfm);

            // Act
           var actualRestoreSpec = isV2Nominate ?
                        await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                        await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);

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
                .ProjectRestoreInfo;
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);
            var metadata = actualRestoreSpec.Projects.Single().RestoreMetadata;

            // Assert
            metadata.Sources.Select(e => e.Source).ShouldBeEquivalentTo(new[] { "base", VSRestoreSettingsUtilities.AdditionalValue, "a", "b", "d" });
            metadata.FallbackFolders.ShouldBeEquivalentTo(new[] { "base", VSRestoreSettingsUtilities.AdditionalValue, "x", "y", "z" });
        }

        [Fact]
        public async Task NominateProjectAsync_PRI2_VerifySourcesAreCombinedAcrossFrameworks()
        {
            var cps = NewCpsProject(@"{ }");
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectSources", "a;d"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", "x;z")}))
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectSources", "b"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", "y")}))
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "netcoreapp2.1",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectSources", "b"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", "y")}))
                .ProjectRestoreInfo2;
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
                .ProjectRestoreInfo;
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);
            var metadata = actualRestoreSpec.Projects.Single().RestoreMetadata;

            // Assert
            metadata.Sources.Select(e => e.Source).ShouldBeEquivalentTo(new[] { "base", VSRestoreSettingsUtilities.AdditionalValue, "a" });
            metadata.FallbackFolders.ShouldBeEquivalentTo(new[] { "base" });
        }

        [Fact]
        public async Task NominateProjectAsync_PRI2_VerifyExcludedFallbackFoldersAreRemoved()
        {
            var cps = NewCpsProject(@"{ }");
            var pri = cps.Builder
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", "x")})) // Add FF
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectSources", "a"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFoldersExcludes", "x")})) // Remove FF
                .ProjectRestoreInfo2;
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
                .ProjectRestoreInfo;
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Find the tool spec
            var metadata = actualRestoreSpec.Projects.Single(e => e.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool).RestoreMetadata;

            // Assert
            metadata.Sources.Select(e => e.Source).ShouldBeEquivalentTo(new[] { "base", VSRestoreSettingsUtilities.AdditionalValue, "a" });
            metadata.FallbackFolders.ShouldBeEquivalentTo(new[] { "base", VSRestoreSettingsUtilities.AdditionalValue, "x" });
        }

        [Fact]
        public async Task NominateProjectAsync_PRI2_VerifyToolRestoresUseAdditionalSourcesAndFallbackFolders()
        {
            var cps = NewCpsProject(@"{ }");
            var pri = cps.Builder
                .WithTool("ToolTest", "2.0.0")
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", "x;y")}))
                .WithTargetFrameworkInfo(
                    new VsTargetFrameworkInfo2(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", "base"),
                                new VsProjectProperty("RestoreFallbackFolders", "base"),
                                new VsProjectProperty("RestoreAdditionalProjectSources", "a"),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFoldersExcludes", "y")}))
                .ProjectRestoreInfo2;
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
        [InlineData(@"..\source1", @"..\additionalsource", @"..\fallback1", @"..\additionalFallback1", false)]
        [InlineData(@"..\source1", null, @"..\fallback1", null, false)]
        [InlineData(null, @"..\additionalsource", null, @"..\additionalFallback1", false)]
        [InlineData(@"Clear;C:\source1", @"C:\additionalsource", @"C:\fallback1;Clear", @"C:\additionalFallback1", false)]
        [InlineData(@"C:\source1;Clear", @"C:\additionalsource", @"Clear;C:\fallback1", @"C:\additionalFallback1", false)]
        [InlineData(@"..\source1", @"..\additionalsource", @"..\fallback1", @"..\additionalFallback1", true)]
        [InlineData(@"..\source1", null, @"..\fallback1", null, true)]
        [InlineData(null, @"..\additionalsource", null, @"..\additionalFallback1", true)]
        [InlineData(@"Clear;C:\source1", @"C:\additionalsource", @"C:\fallback1;Clear", @"C:\additionalFallback1", true)]
        [InlineData(@"C:\source1;Clear", @"C:\additionalsource", @"Clear;C:\fallback1", @"C:\additionalFallback1", true)]
        public async Task NominateProjectAsync_WithRestoreAdditionalSourcesAndFallbackFolders(string restoreSources, string restoreAdditionalProjectSources, string restoreFallbackFolders, string restoreAdditionalFallbackFolders, bool isV2Nominate)
        {
            var vstfms = isV2Nominate ?
                (IVsTargetFrameworkInfo)
                new VsTargetFrameworkInfo2(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", restoreSources),
                                new VsProjectProperty("RestoreFallbackFolders", restoreFallbackFolders),
                                new VsProjectProperty("RestoreAdditionalProjectSources", restoreAdditionalProjectSources),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", restoreAdditionalFallbackFolders)}) :
                new VsTargetFrameworkInfo(
                        "netcoreapp2.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] { new VsProjectProperty("RestoreSources", restoreSources),
                                new VsProjectProperty("RestoreFallbackFolders", restoreFallbackFolders),
                                new VsProjectProperty("RestoreAdditionalProjectSources", restoreAdditionalProjectSources),
                                new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", restoreAdditionalFallbackFolders)});

            var cps = NewCpsProject(@"{ }");
            var builder = cps.Builder
                .WithTool("Foo.Test", "2.0.0")
                .WithTargetFrameworkInfo(vstfms);

            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = isV2Nominate ?
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);

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
        [InlineData(@"C:\source1", @"C:\additionalsource",@"C:\source1;C:\additionalsource", @"C:\fallback1", @"C:\additionalFallback1", @"C:\fallback1;C:\additionalFallback1", false)]
        [InlineData(@"C:\source1", null, @"C:\source1", @"C:\fallback1", null, @"C:\fallback1", false)]
        [InlineData(null, @"C:\additionalsource", @"C:\additionalsource",  null, @"C:\additionalFallback1", @"C:\additionalFallback1", false)]
        [InlineData(@"Clear;C:\source1", @"C:\additionalsource", @"C:\additionalsource",  @"C:\fallback1;Clear", @"C:\additionalFallback1", @"C:\additionalFallback1", false)]
        [InlineData(@"C:\source1;Clear", @"C:\additionalsource", @"C:\additionalsource", @"Clear;C:\fallback1", @"C:\additionalFallback1", @"C:\additionalFallback1", false)]
        [InlineData(@"C:\source1", @"C:\additionalsource", @"C:\source1;C:\additionalsource", @"C:\fallback1", @"C:\additionalFallback1", @"C:\fallback1;C:\additionalFallback1", true)]
        [InlineData(@"C:\source1", null, @"C:\source1", @"C:\fallback1", null, @"C:\fallback1", true)]
        [InlineData(null, @"C:\additionalsource", @"C:\additionalsource", null, @"C:\additionalFallback1", @"C:\additionalFallback1", true)]
        [InlineData(@"Clear;C:\source1", @"C:\additionalsource", @"C:\additionalsource", @"C:\fallback1;Clear", @"C:\additionalFallback1", @"C:\additionalFallback1", true)]
        [InlineData(@"C:\source1;Clear", @"C:\additionalsource", @"C:\additionalsource", @"Clear;C:\fallback1", @"C:\additionalFallback1", @"C:\additionalFallback1", true)]
        public async Task VSSolutionRestoreService_VSRestoreSettingsUtilities_Integration(string restoreSources, string restoreAdditionalProjectSources, string expectedRestoreSources, string restoreFallbackFolders, string restoreAdditionalFallbackFolders, string expectedFallbackFolders, bool isV2Nominate)
        {

            var vstfms = isV2Nominate ?
                        (IVsTargetFrameworkInfo)
                        new VsTargetFrameworkInfo2(
                            "netcoreapp2.0",
                            Enumerable.Empty<IVsReferenceItem>(),
                            Enumerable.Empty<IVsReferenceItem>(),
                            Enumerable.Empty<IVsReferenceItem>(),
                            Enumerable.Empty<IVsReferenceItem>(),
                            new[] { new VsProjectProperty("RestoreSources", restoreSources),
                                    new VsProjectProperty("RestoreFallbackFolders", restoreFallbackFolders),
                                    new VsProjectProperty("RestoreAdditionalProjectSources", restoreAdditionalProjectSources),
                                    new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", restoreAdditionalFallbackFolders)}) :
                        new VsTargetFrameworkInfo(
                            "netcoreapp2.0",
                            Enumerable.Empty<IVsReferenceItem>(),
                            Enumerable.Empty<IVsReferenceItem>(),
                            new[] { new VsProjectProperty("RestoreSources", restoreSources),
                                    new VsProjectProperty("RestoreFallbackFolders", restoreFallbackFolders),
                                    new VsProjectProperty("RestoreAdditionalProjectSources", restoreAdditionalProjectSources),
                                    new VsProjectProperty("RestoreAdditionalProjectFallbackFolders", restoreAdditionalFallbackFolders)});

            var cps = NewCpsProject(@"{ }");
            var builder = cps.Builder
                .WithTool("Foo.Test", "2.0.0")
                .WithTargetFrameworkInfo(vstfms);
            var projectFullPath = cps.ProjectFullPath;

            // Act
            var actualRestoreSpec = isV2Nominate ?
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);

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
        [InlineData("true", null, "false", false)]
        [InlineData(null, "packages.A.lock.json", null, false)]
        [InlineData("true", null, "true", false)]
        [InlineData("false", null, "false", false)]
        [InlineData("true", null, "false", true)]
        [InlineData(null, "packages.A.lock.json", null, true)]
        [InlineData("true", null, "true", true)]
        [InlineData("false", null, "false", true)]
        public async Task NominateProjectAsync_LockFileSettings(
            string restorePackagesWithLockFile,
            string lockFilePath,
            string restoreLockedMode,
            bool isV2Nominate)
        {
            var vstfms = isV2Nominate ?
                (IVsTargetFrameworkInfo)
                new VsTargetFrameworkInfo2(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {
                            new VsProjectProperty("RestorePackagesWithLockFile", restorePackagesWithLockFile),
                            new VsProjectProperty("NuGetLockFilePath", lockFilePath),
                            new VsProjectProperty("RestoreLockedMode", restoreLockedMode)}) :
                new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {
                            new VsProjectProperty("RestorePackagesWithLockFile", restorePackagesWithLockFile),
                            new VsProjectProperty("NuGetLockFilePath", lockFilePath),
                            new VsProjectProperty("RestoreLockedMode", restoreLockedMode)});
            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var builder = cps.Builder
                .WithTargetFrameworkInfo(vstfms);

            // Act
            var actualRestoreSpec = isV2Nominate ?
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal(restorePackagesWithLockFile, actualProjectSpec.RestoreMetadata.RestoreLockProperties.RestorePackagesWithLockFile);
            Assert.Equal(lockFilePath, actualProjectSpec.RestoreMetadata.RestoreLockProperties.NuGetLockFilePath);
            Assert.Equal(MSBuildStringUtility.IsTrue(restoreLockedMode), actualProjectSpec.RestoreMetadata.RestoreLockProperties.RestoreLockedMode);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NominateProjectAsync_RuntimeGraph(bool isV2Nominate)
        {
            var vstfms = isV2Nominate ?
                (IVsTargetFrameworkInfo)
                new VsTargetFrameworkInfo2(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {
                            new VsProjectProperty("RuntimeIdentifier", "win10-x64")
                        }) :
                new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {
                            new VsProjectProperty("RuntimeIdentifier", "win10-x64")
                        });

            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var builder = cps.Builder
                .WithTargetFrameworkInfo(vstfms);

            // Act
            var actualRestoreSpec = isV2Nominate ?
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.Equal("win10-x64", actualProjectSpec.RuntimeGraph.Runtimes.Single().Key);
        }

        [Fact]
        public async Task NominateProjectAsync_BasicPackageDownload()
        {
            var consoleAppProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp3.0"": {
            ""dependencies"": {
                ""NuGet.Protocol"": {
                    ""target"": ""Package"",
                    ""version"": ""5.1.0""
                },
            },
            ""downloadDependencies"": [
                {""name"" : ""NetCoreTargetingPack"", ""version"" : ""[1.0.0]""},
                {""name"" : ""NetCoreRuntimePack"", ""version"" : ""[1.0.0]""},
                {""name"" : ""NetCoreApphostPack"", ""version"" : ""[1.0.0]""},
            ]
            }
        }
    }";
            var projectName = "ConsoleApp1";
            var cps = NewCpsProject(consoleAppProjectJson, projectName);
            var projectFullPath = cps.ProjectFullPath;
            var expectedBaseIntermediate = cps.ProjectRestoreInfo2.BaseIntermediatePath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo2);

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
            Assert.Equal(expectedBaseIntermediate, actualMetadata.OutputPath);

            Assert.Single(actualProjectSpec.TargetFrameworks);
            var actualTfi = actualProjectSpec.TargetFrameworks.Single();

            var expectedFramework = NuGetFramework.Parse("netcoreapp3.0");
            Assert.Equal(expectedFramework, actualTfi.FrameworkName);

            AssertPackages(actualTfi,
                "NuGet.Protocol:5.1.0");
            AssertDownloadPackages(actualTfi,
                "NetCoreTargetingPack:[1.0.0]",
                "NetCoreRuntimePack:[1.0.0]",
                "NetCoreApphostPack:[1.0.0]"
                );
        }

        [Fact]
        public async Task NominateProjectAsync_PackageDownload_AllowsDuplicateIds()
        {
            var consoleAppProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp3.0"": {
            ""dependencies"": {
                ""NuGet.Protocol"": {
                    ""target"": ""Package"",
                    ""version"": ""5.1.0""
                },
            },
            ""downloadDependencies"": [
                {""name"" : ""NetCoreTargetingPack"", ""version"" : ""[1.0.0]""},
                {""name"" : ""NetCoreTargetingPack"", ""version"" : ""[2.0.0]""},
            ]
            }
        }
    }";
            var projectName = "ConsoleApp1";
            var cps = NewCpsProject(consoleAppProjectJson, projectName);
            var projectFullPath = cps.ProjectFullPath;
            var expectedBaseIntermediate = cps.ProjectRestoreInfo2.BaseIntermediatePath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo2);

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
            Assert.Equal(expectedBaseIntermediate, actualMetadata.OutputPath);

            Assert.Single(actualProjectSpec.TargetFrameworks);
            var actualTfi = actualProjectSpec.TargetFrameworks.Single();

            var expectedFramework = NuGetFramework.Parse("netcoreapp3.0");
            Assert.Equal(expectedFramework, actualTfi.FrameworkName);

            AssertPackages(actualTfi,
                "NuGet.Protocol:5.1.0");
            AssertDownloadPackages(actualTfi,
                "NetCoreTargetingPack:[1.0.0]",
                "NetCoreTargetingPack:[2.0.0]"
                );
        }

        [Fact]
        public async Task NominateProjectAsync_PackageDownload_InexactVersionsNotAllowed()
        {
            var consoleAppProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp3.0"": {
            ""dependencies"": {
                ""NuGet.Protocol"": {
                    ""target"": ""Package"",
                    ""version"": ""5.1.0""
                },
            },
            ""downloadDependencies"": [
                {""name"" : ""NetCoreTargetingPack"", ""version"" : ""[1.0.0, )""},
            ]
            }
        }
    }";
            var projectName = "ConsoleApp1";
            var cps = NewCpsProject(consoleAppProjectJson, projectName);
            var projectFullPath = cps.ProjectFullPath;
            var cache = Mock.Of<IProjectSystemCache>();
            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();

            var service = new VsSolutionRestoreService(
                cache, restoreWorker, NullLogger.Instance);

            var result = await service.NominateProjectAsync(projectFullPath, cps.ProjectRestoreInfo2, CancellationToken.None);

            Assert.False(result, "Project restore nomination must fail.");
        }

        [Fact]
        public async Task NominateProjectAsync_BasicFrameworkReferences()
        {
            var consoleAppProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp3.0"": {
            ""dependencies"": {
                ""NuGet.Protocol"": {
                    ""target"": ""Package"",
                    ""version"": ""5.1.0""
                },
            },
            ""frameworkReferences"": [
                ""Microsoft.WindowsDesktop.App|WPF"",
                ""Microsoft.WindowsDesktop.App|WinForms""
            ]
            }
        }
    }";
            var projectName = "ConsoleApp1";
            var cps = NewCpsProject(consoleAppProjectJson, projectName);
            var projectFullPath = cps.ProjectFullPath;
            var expectedBaseIntermediate = cps.ProjectRestoreInfo2.BaseIntermediatePath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo2);

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
            Assert.Equal(expectedBaseIntermediate, actualMetadata.OutputPath);

            Assert.Single(actualProjectSpec.TargetFrameworks);
            var actualTfi = actualProjectSpec.TargetFrameworks.Single();

            var expectedFramework = NuGetFramework.Parse("netcoreapp3.0");
            Assert.Equal(expectedFramework, actualTfi.FrameworkName);

            AssertFrameworkReferences(actualTfi,
                "Microsoft.WindowsDesktop.App|WPF",
                "Microsoft.WindowsDesktop.App|WinForms");
        }

        [Fact]
        public async Task NominateProjectAsync_FrameworkReferencesAreCaseInsensitive()
        {
            var consoleAppProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp3.0"": {
            ""dependencies"": {
                ""NuGet.Protocol"": {
                    ""target"": ""Package"",
                    ""version"": ""5.1.0""
                },
            },
            ""frameworkReferences"": [
                ""Microsoft.WindowsDesktop.App|WinForms"",
                ""Microsoft.WindowsDesktop.App|WINFORMS""
            ]
            }
        }
    }";
            var projectName = "ConsoleApp1";
            var cps = NewCpsProject(consoleAppProjectJson, projectName);
            var projectFullPath = cps.ProjectFullPath;
            var expectedBaseIntermediate = cps.ProjectRestoreInfo2.BaseIntermediatePath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo2);

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
            Assert.Equal(expectedBaseIntermediate, actualMetadata.OutputPath);

            Assert.Single(actualProjectSpec.TargetFrameworks);
            var actualTfi = actualProjectSpec.TargetFrameworks.Single();

            var expectedFramework = NuGetFramework.Parse("netcoreapp3.0");
            Assert.Equal(expectedFramework, actualTfi.FrameworkName);

            AssertFrameworkReferences(actualTfi,
                "Microsoft.WindowsDesktop.App|WinForms");
        }

        [Fact]
        public async Task NominateProjectAsync_FrameworkReferencesMultiTargeting()
        {
            var consoleAppProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp3.0"": {
            ""frameworkReferences"": [
                ""Microsoft.WindowsDesktop.App|WPF"",
                ""Microsoft.WindowsDesktop.App|WinForms""
            ]
        },
         ""netcoreapp3.1"": {
            ""frameworkReferences"": [
                ""Microsoft.ASPNetCore.App""
            ]
            }
        }
        },
    }";
            var projectName = "ConsoleApp1";
            var cps = NewCpsProject(consoleAppProjectJson, projectName);
            var projectFullPath = cps.ProjectFullPath;
            var expectedBaseIntermediate = cps.ProjectRestoreInfo2.BaseIntermediatePath;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, cps.ProjectRestoreInfo2);

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
            Assert.Equal(expectedBaseIntermediate, actualMetadata.OutputPath);

            Assert.Equal(2, actualProjectSpec.TargetFrameworks.Count);

            var tfi = actualProjectSpec.TargetFrameworks.First();

            var expectedFramework = NuGetFramework.Parse("netcoreapp3.0");
            Assert.Equal(expectedFramework, tfi.FrameworkName);

            AssertFrameworkReferences(tfi,
                "Microsoft.WindowsDesktop.App|WPF",
                "Microsoft.WindowsDesktop.App|WinForms");

            tfi = actualProjectSpec.TargetFrameworks.Last();
            expectedFramework = NuGetFramework.Parse("netcoreapp3.1");
            Assert.Equal(expectedFramework, tfi.FrameworkName);

            AssertFrameworkReferences(tfi,
                "Microsoft.ASPNetCore.App");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NominateProjectAsync_WarningProperties(bool  isV2Nominate)
        {
            var packageReference = new VsReferenceItem("NuGet.Protocol", new VsReferenceProperties() { new VsReferenceProperty("NoWarn", "NU1605") } );

            var vstfms = isV2Nominate ?
                (IVsTargetFrameworkInfo)
                new VsTargetFrameworkInfo2(
                        "netcoreapp1.0",
                        new IVsReferenceItem[] { packageReference },
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {
                            new VsProjectProperty("TreatWarningsAsErrors", "true")
                        }) :
                new VsTargetFrameworkInfo(
                        "netcoreapp1.0",
                        new IVsReferenceItem[] { packageReference },
                        Enumerable.Empty<IVsReferenceItem>(),
                        new[] {
                            new VsProjectProperty("TreatWarningsAsErrors", "true")
                        });

            var cps = NewCpsProject("{ }");
            var projectFullPath = cps.ProjectFullPath;
            var builder = cps.Builder
                .WithTargetFrameworkInfo(vstfms);

            // Act
            var actualRestoreSpec = isV2Nominate ?
                    await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo2) :
                    await CaptureNominateResultAsync(projectFullPath, builder.ProjectRestoreInfo);
            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);
            Assert.True(actualProjectSpec.RestoreMetadata.ProjectWideWarningProperties.AllWarningsAsErrors);
            Assert.True(actualProjectSpec.TargetFrameworks.First().Dependencies.First().NoWarn.First().Equals(NuGetLogCode.NU1605));
        }

        [Fact]
        public void NominateProjectAsync_ThrowsNullReferenceException()
        {
            var cache = Mock.Of<IProjectSystemCache>();

            Mock.Get(cache)
                .Setup(x => x.AddProjectRestoreInfo(
                    It.IsAny<ProjectNames>(),
                    It.IsAny<DependencyGraphSpec>()))
                .Returns(true);

            var completedRestoreTask = Task.FromResult(true);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();

            var service = new VsSolutionRestoreService(
                cache, restoreWorker, NullLogger.Instance);

            // Act
            _ = Assert.ThrowsAsync<ArgumentNullException>(async () => await service.NominateProjectAsync(@"F:\project\project.csproj", (IVsProjectRestoreInfo)null, CancellationToken.None));

            _ = Assert.ThrowsAsync<ArgumentNullException>(async () => await service.NominateProjectAsync(@"F:\project\project.csproj", (IVsProjectRestoreInfo2)null, CancellationToken.None));


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

        private async Task<DependencyGraphSpec> CaptureNominateResultAsync(
            string projectFullPath, IVsProjectRestoreInfo2 pri)
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
                cache, restoreWorker, NullLogger.Instance);

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

        private static void AssertDownloadPackages(TargetFrameworkInformation actualTfi, params string[] expectedPackages)
        {
            var actualPackages = actualTfi
                .DownloadDependencies
                .Select(ld => $"{ld.Name}:{ld.VersionRange.OriginalString}");

            Assert.Equal(expectedPackages, actualPackages);
        }

        private static void AssertFrameworkReferences(TargetFrameworkInformation actualTfi, params string[] expectedPackages)
        {
            Assert.Equal(expectedPackages, actualTfi.FrameworkReferences);
        }

        private class TestContext
        {
            public string ProjectFullPath { get; set; }
            public ProjectRestoreInfoBuilder Builder { get; set; }

            public VsProjectRestoreInfo ProjectRestoreInfo => Builder.ProjectRestoreInfo;

            public VsProjectRestoreInfo2 ProjectRestoreInfo2 => Builder.ProjectRestoreInfo2;

        }
    }
}
