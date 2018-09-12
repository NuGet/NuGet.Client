// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.Test
{
    public class DependencyGraphRestoreUtilityTests
    {
        [Fact]
        public async Task DependencyGraphRestoreUtility_NoopRestoreTest()
        {
            // Arrange
            var projectName = "testproj";
            var logger = new TestLogger();

            using (var rootFolder = TestDirectory.Create())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                var sources = new[] {
                    Repository.Factory.GetVisualStudio(new PackageSource("https://www.nuget.org/api/v2/"))
                };

                var targetFramework = NuGetFramework.Parse("net46");

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(targetFramework, new TestNuGetProjectContext());
                var project = new TestMSBuildNuGetProject(msBuildNuGetProjectSystem, rootFolder, projectFolder.FullName);

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                var restoreContext = new DependencyGraphCacheContext(logger, NullSettings.Instance);

                var projects = new List<IDependencyGraphProject>() { project };

                var solutionManager = new TestSolutionManager(false);
                solutionManager.NuGetProjects.Add(project);

                // Act
                await DependencyGraphRestoreUtility.RestoreAsync(
                    solutionManager,
                    await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(solutionManager, restoreContext),
                    restoreContext,
                    new RestoreCommandProvidersCache(),
                    (c) => { },
                    sources,
                    Guid.Empty,
                    false,
                    true,
                    logger,
                    CancellationToken.None);

                // Assert
                Assert.Equal(0, logger.Errors);
                Assert.Equal(0, logger.Warnings);
            }
        }
    }
}