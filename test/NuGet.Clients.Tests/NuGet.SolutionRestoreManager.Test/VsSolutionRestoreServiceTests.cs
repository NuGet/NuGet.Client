// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.SolutionRestoreManager.Test
{
    public class VsSolutionRestoreServiceTests:IDisposable
    {
        private readonly TestDirectory _testDirectory;

        static VsSolutionRestoreServiceTests()
        {
            var mainThread = Thread.CurrentThread;
            var synchronizationContext = SynchronizationContext.Current;
            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(mainThread, synchronizationContext);
        }

        public VsSolutionRestoreServiceTests()
        {
            _testDirectory = TestDirectory.Create();
        }

        public void Dispose()
        {
            _testDirectory.Dispose();
        }

        [Fact]
        public async Task NominateProjectAsync_ConsoleAppTemplate_Succeeds()
        {
            var projectLocation = _testDirectory.Path;
            var projectName = "ConsoleApp1";
            var projectFullPath = Path.Combine(projectLocation, $"{projectName}.csproj");

            var baseIntermediatePath = Path.Combine(projectLocation, "obj");
            Directory.CreateDirectory(baseIntermediatePath);

            var consoleAppProjectJson = $@"{{
    ""frameworks"": {{
        ""netcoreapp1.0"": {{
            ""dependencies"": {{
                ""Microsoft.NET.Sdk"": {{
                    ""target"": ""Package"",
                    ""version"": ""1.0.0-alpha-20161019-1""
                }},
                ""Microsoft.NETCore.App"": {{
                    ""target"": ""Package"",
                    ""version"": ""1.0.1""
                }}
            }}
        }}
    }}
}}";

            var spec = JsonPackageSpecReader.GetPackageSpec(consoleAppProjectJson, projectName, projectFullPath);

            var pri = ProjectRestoreInfoBuilder.Build(spec, baseIntermediatePath);

            var dte = Mock.Of<EnvDTE.DTE>();

            var serviceProvider = Mock.Of<IServiceProvider>();
            Mock.Get(serviceProvider)
                .Setup(x => x.GetService(typeof(EnvDTE.DTE)))
                .Returns(dte);

            var cache = Mock.Of<IProjectSystemCache>();

            var dteProject = Mock.Of<EnvDTE.Project>();
            Mock.Get(dteProject)
                .SetupGet(x => x.UniqueName)
                .Returns(projectFullPath);
            Mock.Get(dteProject)
                .SetupGet(x => x.Name)
                .Returns(projectName);

            Mock.Get(cache)
                .Setup(x => x.TryGetDTEProject(projectFullPath, out dteProject))
                .Returns(true);

            PackageSpec actualRestoreSpec = null;

            Mock.Get(cache)
                .Setup(x => x.AddProjectRestoreInfo(projectFullPath,
                    It.IsAny<PackageSpec>(), It.IsAny<ProjectNames>()))
                .Callback<ProjectNames, PackageSpec>(
                    (_, ps) => { actualRestoreSpec = ps; }
                )
                .Returns(true);

            var restoreWorker = Mock.Of<ISolutionRestoreWorker>();
            Mock.Get(restoreWorker)
                .Setup(x => x.ScheduleRestoreAsync(
                    It.IsAny<SolutionRestoreRequest>(), CancellationToken.None))
                .ReturnsAsync(true);

            var service = new VsSolutionRestoreService(
                serviceProvider, cache, restoreWorker, NuGet.Common.NullLogger.Instance);

            // Act
            var result = await service.NominateProjectAsync(projectFullPath, pri, CancellationToken.None);

            Assert.True(result, "Project restore nomination should succeed.");
            Assert.NotNull(actualRestoreSpec?.RestoreMetadata);

            var actualMetadata = actualRestoreSpec.RestoreMetadata;
            Assert.Equal(projectFullPath, actualMetadata.ProjectPath);
            Assert.Equal(projectName, actualMetadata.ProjectName);
            Assert.Equal(RestoreOutputType.NETCore, actualMetadata.OutputType);
            Assert.Equal(baseIntermediatePath, actualMetadata.OutputPath);

            Assert.Single(actualRestoreSpec.TargetFrameworks);
            var actualTfi = actualRestoreSpec.TargetFrameworks.Single();

            var expectedFramework = NuGetFramework.Parse("netcoreapp1.0");
            Assert.Equal(expectedFramework, actualTfi.FrameworkName);

            var expectedPackages = new[]
            {
                "Microsoft.NET.Sdk:1.0.0-alpha-20161019-1",
                "Microsoft.NETCore.App:1.0.1"
            };
            var actualPackages = actualTfi.Dependencies
                .Where(ld => ld.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package)
                .Select(ld => $"{ld.Name}:{ld.LibraryRange.VersionRange.OriginalString}");
            Assert.Equal(expectedPackages, actualPackages);

            Mock.Get(restoreWorker)
                .Verify(
                    x => x.ScheduleRestoreAsync(It.IsAny<SolutionRestoreRequest>(), CancellationToken.None), 
                    Times.Once());
        }
    }
}
