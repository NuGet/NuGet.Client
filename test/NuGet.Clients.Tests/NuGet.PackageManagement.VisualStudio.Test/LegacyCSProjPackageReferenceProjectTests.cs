// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Moq;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class LegacyCSProjPackageReferenceProjectTests
    {
        [Fact]
        public async Task LCPRP_AssetsFileLocation()
        {
            // Arrange
            using (var randomTestFolder = TestDirectory.Create())
            {
                var testBaseIntermediateOutputPath = Path.Combine(randomTestFolder, "obj");
                TestDirectory.Create(testBaseIntermediateOutputPath);
                var testEnvDTEProjectAdapter = new Mock<IEnvDTEProjectAdapter>();
                testEnvDTEProjectAdapter
                    .Setup(x => x.BaseIntermediateOutputPath)
                    .Returns(testBaseIntermediateOutputPath);

                var testProject = new LegacyCSProjPackageReferenceProject(
                    project: testEnvDTEProjectAdapter.Object, 
                    projectId: String.Empty, 
                    callerIsUnitTest: true);

                // Act
                var assetsPath = await testProject.GetAssetsFilePathAsync();

                // Assert
                Assert.Equal(Path.Combine(testBaseIntermediateOutputPath, "project.assets.json"), assetsPath);
            }
        }

        [Fact]
        public async Task LCPRP_PackageTargetFallback()
        {
            // Arrange
            using (var randomTestFolder = TestDirectory.Create())
            {
                var testEnvDTEProjectAdapter = new EnvDTEProjectAdapterMock(randomTestFolder);
                testEnvDTEProjectAdapter
                    .Setup(x => x.PackageTargetFallback)
                    .Returns("portable-net45+win8;dnxcore50");
                testEnvDTEProjectAdapter
                    .Setup(x => x.TargetNuGetFramework)
                    .Returns(new NuGetFramework("netstandard13"));

                var testProject = new LegacyCSProjPackageReferenceProject(
                    project: testEnvDTEProjectAdapter.Object,
                    projectId: "",
                    callerIsUnitTest: true);

                var testDependencyGraphCacheContext = new DependencyGraphCacheContext();

                // Act
                var installedPackages = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.Equal(
                    new List<NuGetFramework>() { NuGetFramework.Parse("portable-net45+win8"), NuGetFramework.Parse("dnxcore50") },
                    installedPackages.First().TargetFrameworks.First().Imports.ToList());
                Assert.IsType(typeof(FallbackFramework), installedPackages.First().TargetFrameworks.First().FrameworkName);
                Assert.Equal(new List<NuGetFramework>() { NuGetFramework.Parse("portable-net45+win8"), NuGetFramework.Parse("dnxcore50") },
                    ((FallbackFramework)(installedPackages.First().TargetFrameworks.First().FrameworkName)).Fallback);
            }
        }

        [Fact]
        public async Task LCPRP_PackageVersion()
        {
            // Arrange
            using (var randomTestFolder = TestDirectory.Create())
            {
                var testEnvDTEProjectAdapter = new EnvDTEProjectAdapterMock(randomTestFolder);
                testEnvDTEProjectAdapter
                    .Setup(x => x.TargetNuGetFramework)
                    .Returns(new NuGetFramework("netstandard13"));
                testEnvDTEProjectAdapter
                    .Setup(x => x.Version)
                    .Returns("2.2.3");

                var testProject = new LegacyCSProjPackageReferenceProject(
                    project: testEnvDTEProjectAdapter.Object,
                    projectId: "",
                    callerIsUnitTest: true);

                var testDependencyGraphCacheContext = new DependencyGraphCacheContext();

                // Act
                var installedPackages = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.Equal(new NuGetVersion("2.2.3"), installedPackages.First().Version);
            }
        }

        [Fact]
        public async Task LCPRP_PackageVersion_Default()
        {
            // Arrange
            using (var randomTestFolder = TestDirectory.Create())
            {
                var testEnvDTEProjectAdapter = new EnvDTEProjectAdapterMock(randomTestFolder);
                testEnvDTEProjectAdapter
                    .Setup(x => x.TargetNuGetFramework)
                    .Returns(new NuGetFramework("netstandard13"));

                var testProject = new LegacyCSProjPackageReferenceProject(
                    project: testEnvDTEProjectAdapter.Object,
                    projectId: "",
                    callerIsUnitTest: true);

                var testDependencyGraphCacheContext = new DependencyGraphCacheContext();

                // Act
                var installedPackages = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.Equal(new NuGetVersion("1.0.0"), installedPackages.First().Version);
            }
        }

        private class EnvDTEProjectAdapterMock : Mock<IEnvDTEProjectAdapter>
        {
            public EnvDTEProjectAdapterMock()
            {
                Setup(x => x.GetLegacyCSProjPackageReferences(It.IsAny<Array>()))
                    .Returns(Array.Empty<LegacyCSProjPackageReference>);
                Setup(x => x.GetLegacyCSProjProjectReferences(It.IsAny<Array>()))
                    .Returns(Array.Empty<LegacyCSProjProjectReference>);
                Setup(x => x.Runtimes)
                    .Returns(Enumerable.Empty<RuntimeDescription>);
                Setup(x => x.Supports)
                    .Returns(Enumerable.Empty<CompatibilityProfile>);
                Setup(x => x.Version)
                    .Returns("1.0.0");
            }

            public EnvDTEProjectAdapterMock(string fullPath): this()
            {
                Setup(x => x.ProjectFullPath)
                    .Returns(Path.Combine(fullPath, "foo.csproj"));

                var testBaseIntermediateOutputPath = Path.Combine(fullPath, "obj");
                TestDirectory.Create(testBaseIntermediateOutputPath);
                Setup(x => x.BaseIntermediateOutputPath)
                    .Returns(testBaseIntermediateOutputPath);
            }
        }
    }
}
