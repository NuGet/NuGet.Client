// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using NuGet.VisualStudio;
using NuGet.Test.Utility;
using System.Threading;
using System.IO;
using Moq;
using FluentAssertions;
using NuGet.ProjectModel;
using NuGet.Commands.Test;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class NetCorePackageReferenceProjectTests
    {
        [Fact]
        public async Task GetInstalledVersion_WithAssetsFile_ReturnsVersionsFromAssetsSpecs()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateNetCorePackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "3.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("3.0.0"))));
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithFloating_WithAssetsFile_ReturnsVersionsFromAssetsSpecs()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateNetCorePackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(projectName, projectFullPath, "[*, )");

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "4.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("4.0.0"))));

                var cache_packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                cache_packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("4.0.0"))));
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithoutAssetsFile_ReturnsVersionsFromPackageSpecs()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateNetCorePackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();

                // Act
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.0.0"))));

                var cache_packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                cache_packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.0.0"))));
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithoutPackages_ReturnsEmpty()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateNetCorePackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpecNoPackages(projectName, projectFullPath);

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                packages.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithASpecificVersionLowerThanAvailableOne_ReturnsVersionFromAssetsFile()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateNetCorePackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "4.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                var exists = packages.Where(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("4.0.0"))));
                Assert.True(exists.Count() == 1);
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithoutPackages_WithAssets_ReturnsEmpty()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateNetCorePackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpecNoPackages(projectName, projectFullPath);

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "1.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                packages.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task GetInstalledVersions_WhenCalledMultipleTimes_ReturnsSameResult()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateNetCorePackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpecMultipleVersions(projectName, projectFullPath);

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "3.0.0");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageB", "4.0.0");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageC", "1.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("3.0.0"))));
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageB", new NuGetVersion("4.0.0"))));
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageC", new NuGetVersion("1.0.0"))));

                var cache_packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                cache_packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("3.0.0"))));
                cache_packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                cache_packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageB", new NuGetVersion("4.0.0"))));
                cache_packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                cache_packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageC", new NuGetVersion("1.0.0"))));
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithAssetsFile_ChangingPackageSpec_ReturnsVersionsFromAssetsSpecs()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateNetCorePackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "2.0.0");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "3.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.0.0"))));

                // Setup
                packageSpec = GetPackageSpec(projectName, projectFullPath, "[3.0.0, )");

                // Restore info
                projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                // Act
                command = new RestoreCommand(request);
                result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("3.0.0"))));
            }
        }

        private NetCorePackageReferenceProject CreateNetCorePackageReferenceProject(string projectName, string projectFullPath, ProjectSystemCache projectSystemCache)
        {
            var projectServices = new TestProjectSystemServices();

            return new NetCorePackageReferenceProject(
                    projectName: projectName,
                    projectUniqueName: projectName,
                    projectFullPath: projectFullPath,
                    projectSystemCache: projectSystemCache,
                    unconfiguredProject: null,
                    projectServices: projectServices,
                    projectId: projectName);
        }

        private ProjectNames GetTestProjectNames(string projectPath, string projectUniqueName)
        {
            var projectNames = new ProjectNames(
            fullName: projectPath,
            uniqueName: projectUniqueName,
            shortName: projectUniqueName,
            customUniqueName: projectUniqueName,
            projectId: Guid.NewGuid().ToString());
            return projectNames;
        }

        private static PackageSpec GetPackageSpec(string projectName, string testDirectory, string version)
        {
            string referenceSpec = $@"
                {{
                    ""frameworks"":
                    {{
                        ""net5.0"":
                        {{
                            ""dependencies"":
                            {{
                                ""packageA"":
                                {{
                                    ""version"": ""{version}"",
                                    ""target"": ""Package""
                                }},
                            }}
                        }}
                    }}
                }}";
            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, testDirectory).WithTestRestoreMetadata();
        }

        private static PackageSpec GetPackageSpecNoPackages(string projectName, string testDirectory)
        {
            const string referenceSpec = @"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                }
                            }
                        }
                    }
                }";
            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, testDirectory).WithTestRestoreMetadata();
        }

        private static PackageSpec GetPackageSpecMultipleVersions(string projectName, string testDirectory)
        {
            const string referenceSpec = @"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                    ""packageA"": {
                                    ""version"": ""[*, )"",
                                    ""target"": ""Package""
                                },
                                    ""packageB"": {
                                    ""version"": ""[1.0.0, )"",
                                    ""target"": ""Package""
                                },
                                    ""packageC"": {
                                    ""version"": ""[1.0.0, )"",
                                    ""target"": ""Package""
                                }
                            }
                        }
                    }
                }";
            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, testDirectory).WithTestRestoreMetadata();
        }
    }
}
