// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace ProjectManagement.Test
{
    public class BuildIntegratedNuGetProjectTests
    {
        [Fact]
        public void TestBuildIntegratedNuGetPackageSpecNameMatchesFilePath_ProjectNameJson()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "fileName.project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath,
                    "msbuildName");

                var projectFilePath = Path.Combine(randomProjectFolderPath, "fileName.csproj");

                var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                // Assert
                Assert.Equal(projectFilePath, buildIntegratedProject.MSBuildProjectPath);
                Assert.Equal("fileName", buildIntegratedProject.ProjectName);
                Assert.Equal("fileName", buildIntegratedProject.PackageSpec.Name);
            }
        }

        [Fact]
        public void TestBuildIntegratedNuGetPackageSpecNameMatchesFilePath()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath,
                    "msbuildName");

                var projectFilePath = Path.Combine(randomProjectFolderPath, "fileName.csproj");

                var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                // Assert
                Assert.Equal(projectFilePath, buildIntegratedProject.MSBuildProjectPath);
                Assert.Equal("fileName", buildIntegratedProject.ProjectName);
                Assert.Equal("fileName", buildIntegratedProject.PackageSpec.Name);
            }
        }

        [Fact]
        public async Task TestBuildIntegratedNuGetProjectInstallPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyContentPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await buildIntegratedProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomConfig));
                // Check the number of packages and packages returned by project after the installation
                var installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, installedPackages.Count);
                Assert.Equal(packageIdentity, installedPackages[0].PackageIdentity);
            }
        }

        [Fact]
        public async Task TestBuildIntegratedNuGetProjectUninstallPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var packageIdentity2 = new PackageIdentity("packageB", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyContentPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                var packageFileInfo2 = TestPackagesGroupedByFolder.GetLegacyContentPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    await buildIntegratedProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                    await buildIntegratedProject.InstallPackageAsync(packageIdentity2, packageStream, testNuGetProjectContext, token);

                    // Act
                    await buildIntegratedProject.UninstallPackageAsync(packageIdentity2, new TestNuGetProjectContext(), CancellationToken.None);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomConfig));
                // Check the number of packages and packages returned by project after the installation
                var installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, installedPackages.Count);
                Assert.Equal(packageIdentity, installedPackages[0].PackageIdentity);
            }
        }

        [Fact]
        public async Task TestBuildIntegratedNuGetProjectUninstallAllPackages()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var packageIdentity2 = new PackageIdentity("packageB", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyContentPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                var packageFileInfo2 = TestPackagesGroupedByFolder.GetLegacyContentPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    await buildIntegratedProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                    await buildIntegratedProject.InstallPackageAsync(packageIdentity2, packageStream, testNuGetProjectContext, token);

                    // Act
                    await buildIntegratedProject.UninstallPackageAsync(packageIdentity2, new TestNuGetProjectContext(), CancellationToken.None);
                    await buildIntegratedProject.UninstallPackageAsync(packageIdentity, new TestNuGetProjectContext(), CancellationToken.None);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomConfig));
                // Check the number of packages and packages returned by project after the installation
                var installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, installedPackages.Count);
            }
        }

        private static void CreateConfigJson(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.Write(BasicConfig.ToString());
            }
        }

        private static DownloadResourceResult GetDownloadResourceResult(FileInfo fileInfo)
        {
            return new DownloadResourceResult(fileInfo.OpenRead());
        }

        private static JObject BasicConfig
        {
            get
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["netcore50"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }

        private static void CreateFile(string path)
        {
            File.OpenWrite(path).WriteByte(0);
        }
    }
}
