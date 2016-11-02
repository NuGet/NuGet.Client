// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        public async Task NominateProjectAsync_ConsoleAppTemplate_Succeeds()
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
            var projectFullPath = cps.Item1;

            var cache = Mock.Of<IProjectSystemCache>();

            DependencyGraphSpec actualRestoreSpec = null;

            Mock.Get(cache)
                .Setup(x => x.AddProjectRestoreInfo(
                    It.IsAny<ProjectNames>(),
                    It.IsAny<DependencyGraphSpec>()))
                .Callback<ProjectNames, DependencyGraphSpec>(
                    (_, dg) => { actualRestoreSpec = dg; })
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
            var result = await service.NominateProjectAsync(projectFullPath, cps.Item3, CancellationToken.None);

            Assert.True(result, "Project restore nomination should succeed.");
            Assert.NotNull(actualRestoreSpec);
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            Assert.NotNull(actualRestoreSpec.GetProjectSpec(projectFullPath));
            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);

            var actualMetadata = actualProjectSpec.RestoreMetadata;
            Assert.Equal(projectFullPath, actualMetadata.ProjectPath);
            Assert.Equal(projectName, actualMetadata.ProjectName);
            Assert.Equal(RestoreOutputType.NETCore, actualMetadata.OutputType);
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
        public async Task NominateProjectAsync_WithTools_Succeeds()
        {
            const string toolProjectJson = @"{
    ""tools"": {
        ""Foo.Test.Tools"": ""1.0.0""
    },
    ""frameworks"": {
        ""netcoreapp1.0"": { }
    }
}";
            var cps = NewCpsProject(projectJson: toolProjectJson);
            var projectFullPath = cps.Item1;

            var cache = Mock.Of<IProjectSystemCache>();

            DependencyGraphSpec actualRestoreSpec = null;

            Mock.Get(cache)
                .Setup(x => x.AddProjectRestoreInfo(
                    It.IsAny<ProjectNames>(),
                    It.IsAny<DependencyGraphSpec>()))
                .Callback<ProjectNames, DependencyGraphSpec>(
                    (_, dg) => { actualRestoreSpec = dg; })
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
            var result = await service.NominateProjectAsync(projectFullPath, cps.Item3, CancellationToken.None);

            Assert.True(result, "Project restore nomination should succeed.");
            Assert.NotNull(actualRestoreSpec);
            SpecValidationUtility.ValidateDependencySpec(actualRestoreSpec);

            Assert.NotNull(actualRestoreSpec.GetProjectSpec(projectFullPath));
            var actualProjectSpec = actualRestoreSpec.GetProjectSpec(projectFullPath);

            var actualToolSpec = actualRestoreSpec
                .Projects
                .Where(p => !object.ReferenceEquals(p, actualProjectSpec))
                .Single();
            var actualMetadata = actualToolSpec.RestoreMetadata;
            Assert.NotNull(actualMetadata);
            Assert.Equal(projectFullPath, actualMetadata.ProjectPath);
            Assert.Equal(RestoreOutputType.DotnetCliTool, actualMetadata.OutputType);
            Assert.Null(actualMetadata.OutputPath);
        }

        private Tuple<string, string, IVsProjectRestoreInfo> NewCpsProject(
            string projectName = null, string projectJson = null)
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
            var pri = ProjectRestoreInfoBuilder.Build(spec, baseIntermediatePath);
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
