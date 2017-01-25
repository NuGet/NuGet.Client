// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

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
            var actualRestoreTask = service.NominateProjectAsync(cps.Item1, cps.Item3, CancellationToken.None);

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
            var cps = NewCpsProject(projectName, consoleAppProjectJson);
            var pri = cps.Item3;
            var projectFullPath = cps.Item1;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);

            var actualMetadata = actualProjectSpec.RestoreMetadata;
            Assert.NotNull(actualMetadata);
            Assert.Equal(projectFullPath, actualMetadata.ProjectPath);
            Assert.Equal(projectName, actualMetadata.ProjectName);
            Assert.Equal(ProjectStyle.PackageReference, actualMetadata.ProjectStyle);
            Assert.Equal(cps.Item2, actualMetadata.OutputPath);

            Assert.Single(actualProjectSpec.TargetFrameworks);
            var actualTfi = actualProjectSpec.TargetFrameworks.Single();

            var expectedFramework = NuGetFramework.Parse("netcoreapp1.0");
            Assert.Equal(expectedFramework, actualTfi.FrameworkName);

            AssertPackages(actualTfi,
                "Microsoft.NET.Sdk:1.0.0-alpha-20161019-1",
                "Microsoft.NETCore.App:1.0.1");
        }

        [Fact]
        public async Task NominateProjectAsync_ProjectWithTools()
        {
            const string toolProjectJson = @"{
    ""frameworks"": {
        ""netcoreapp1.0"": { }
    }
}";
            var cps = NewCpsProject(
                projectJson: toolProjectJson,
                tools: new[]
                {
                    new LibraryRange(
                        "Foo.Test.Tools",
                        VersionRange.Parse("2.0.0"),
                        LibraryDependencyTarget.Package)
                });
            var pri = cps.Item3;
            var projectFullPath = cps.Item1;

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
            var actualToolLibrary = actualToolSpec
                .TargetFrameworks
                .Single()
                .Dependencies
                .Single();
            Assert.Equal("Foo.Test.Tools", actualToolLibrary.Name);
            Assert.Equal("2.0.0", actualToolLibrary.LibraryRange.VersionRange.OriginalString);
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
            var pri = cps.Item3;
            var projectFullPath = cps.Item1;
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
            var cps = NewCpsProject(
                projectJson: projectJson);
            var pri = cps.Item3;
            var projectFullPath = cps.Item1;

            // Act
            var actualRestoreSpec = await CaptureNominateResultAsync(projectFullPath, pri);

            // Assert
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);
            Assert.NotNull(actualProjectSpec);

            var actualTfi = actualProjectSpec.TargetFrameworks.Single();
            var actualImports = string.Join(";", actualTfi.Imports.Select(x => x.GetShortFolderName()));
            Assert.Equal("dotnet5.3;portable-net452+win81", actualImports);
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

        private Tuple<string, string, VsProjectRestoreInfo> NewCpsProject(
            string projectName = null,
            string projectJson = null,
            bool crossTargeting = false,
            IEnumerable<LibraryRange> tools = null)
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
            var pri = ProjectRestoreInfoBuilder.Build(
                spec,
                baseIntermediatePath,
                crossTargeting,
                tools);
            return Tuple.Create(projectFullPath, baseIntermediatePath, pri);
        }

        private static IVsReferenceItem ToReferenceItem(string itemName, string versionRange)
        {
            var properties = new VsReferenceProperties(
                new[] { new VsReferenceProperty("Version", versionRange) }
            );
            return new VsReferenceItem(itemName, properties);
        }

        private static void AssertPackages(TargetFrameworkInformation actualTfi, params string[] expectedPackages)
        {
            var actualPackages = actualTfi
                .Dependencies
                .Where(ld => ld.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package)
                .Select(ld => $"{ld.Name}:{ld.LibraryRange.VersionRange.OriginalString}");

            Assert.Equal(expectedPackages, actualPackages);
        }
    }
}
