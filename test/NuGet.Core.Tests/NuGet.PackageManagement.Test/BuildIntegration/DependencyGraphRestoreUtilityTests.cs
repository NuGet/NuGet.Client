// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Test.Utility;
using Test.Utility;
using Test.Utility.ProjectManagement;
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
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                var sources = new[]
                {
                    Repository.Factory.GetVisualStudio(new PackageSource("https://www.nuget.org/api/v2/"))
                };

                var targetFramework = NuGetFramework.Parse("net46");

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(targetFramework, new TestNuGetProjectContext());
                var project = new TestMSBuildNuGetProject(msBuildNuGetProjectSystem, rootFolder, projectFolder.FullName);

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                var restoreContext = new DependencyGraphCacheContext(logger, NullSettings.Instance);

                var projects = new List<IDependencyGraphProject>() { project };

                using (var solutionManager = new TestSolutionManager())
                {
                    solutionManager.NuGetProjects.Add(project);

                    // Act
                    await DependencyGraphRestoreUtility.RestoreAsync(
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

        [Fact]
        public async Task RestoreAsync_WithMinimalProjectAndAdditionalErrorMessage_WritesErrorsToAssetsFile()
        {
            // Arrange
            var projectName = "testproj";
            var logger = new TestLogger();

            using (var rootFolder = TestDirectory.Create())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var objFolder = projectFolder.CreateSubdirectory("obj");
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));
                var globalPackagesFolder = Path.Combine(rootFolder, "gpf");

                var sources = new SourceRepository[0];
                var restoreContext = new DependencyGraphCacheContext(logger, NullSettings.Instance);
                var solutionManager = new Mock<ISolutionManager>();
                var restoreCommandProvidersCache = new RestoreCommandProvidersCache();

                // When a VS nomination results in an exception, we use this minimal DGSpec to do a restore.
                var dgSpec = DependencyGraphSpecTestUtilities.CreateMinimalDependencyGraphSpec(msbuildProjectPath.FullName, objFolder.FullName);
                dgSpec.AddRestore(dgSpec.Projects[0].FilePath);
                // CpsPackageReferenceProject sets some additional properties, from settings, in GetPackageSpecsAndAdditionalMessages(...)
                dgSpec.Projects[0].RestoreMetadata.PackagesPath = globalPackagesFolder;

                // Having an "additional" error message is also critical
                var restoreLogMessage = new RestoreLogMessage(LogLevel.Error, NuGetLogCode.NU1000, "Test error")
                {
                    FilePath = msbuildProjectPath.FullName,
                    ProjectPath = msbuildProjectPath.FullName
                };
                var additionalMessages = new List<IAssetsLogMessage>()
                {
                    AssetsLogMessage.Create(restoreLogMessage)
                };

                // Act
                await DependencyGraphRestoreUtility.RestoreAsync(
                    dgSpec,
                    restoreContext,
                    restoreCommandProvidersCache,
                    cacheContextModifier: _ => { },
                    sources,
                    parentId: Guid.Empty,
                    forceRestore: false,
                    isRestoreOriginalAction: true,
                    additionalMessages,
                    logger,
                    CancellationToken.None);

                // Assert
                var assetsFilePath = Path.Combine(objFolder.FullName, "project.assets.json");
                Assert.True(File.Exists(assetsFilePath), "Assets file does not exist");
                LockFile assetsFile = new LockFileFormat().Read(assetsFilePath);
                IAssetsLogMessage actualMessage = Assert.Single(assetsFile.LogMessages);
                Assert.Equal(restoreLogMessage.Level, actualMessage.Level);
                Assert.Equal(restoreLogMessage.Code, actualMessage.Code);
                Assert.Equal(restoreLogMessage.Message, actualMessage.Message);
            }
        }
    }
}
