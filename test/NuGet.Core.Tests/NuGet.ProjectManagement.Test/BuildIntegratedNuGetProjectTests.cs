// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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
        public void BuildIntegratedNuGetProject_GetLockFilePathWithProjectNameOnly()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projNameFile = Path.Combine(randomProjectFolderPath, "abc.project.json");
                CreateFile(projNameFile);

                // Act
                var path = BuildIntegratedProjectUtility.GetProjectConfigPath(randomProjectFolderPath, "abc");
                var fileName = Path.GetFileName(path);

                // Assert
                Assert.Equal(fileName, "abc.project.json");
            }
        }

        [Fact]
        public void BuildIntegratedNuGetProject_GetLockFilePathWithBothProjectJsonFiles()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projNameFile = Path.Combine(randomProjectFolderPath, "abc.project.json");
                var projJsonFile = Path.Combine(randomProjectFolderPath, "project.json");
                CreateFile(projNameFile);
                CreateFile(projJsonFile);

                // Act
                var path = BuildIntegratedProjectUtility.GetProjectConfigPath(randomProjectFolderPath, "abc");
                var fileName = Path.GetFileName(path);

                // Assert
                Assert.Equal(fileName, "abc.project.json");
            }
        }

        [Fact]
        public void BuildIntegratedNuGetProject_GetLockFilePathWithProjectJsonFromAnotherProject()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projNameFile = Path.Combine(randomProjectFolderPath, "xyz.project.json");
                var projJsonFile = Path.Combine(randomProjectFolderPath, "project.json");
                CreateFile(projNameFile);
                CreateFile(projJsonFile);

                // Act
                var path = BuildIntegratedProjectUtility.GetProjectConfigPath(randomProjectFolderPath, "abc");
                var fileName = Path.GetFileName(path);

                // Assert
                Assert.Equal(fileName, "project.json");

                // Clean-up
            }
        }

        [Fact]
        public void BuildIntegratedNuGetProject_GetLockFilePathWithProjectNameJsonAndAnotherProject()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var otherFile = Path.Combine(randomProjectFolderPath, "xyz.project.json");
                var projJsonFile = Path.Combine(randomProjectFolderPath, "abc.project.json");
                CreateFile(otherFile);
                CreateFile(projJsonFile);

                // Act
                var path = BuildIntegratedProjectUtility.GetProjectConfigPath(randomProjectFolderPath, "abc");
                var fileName = Path.GetFileName(path);

                // Assert
                Assert.Equal(fileName, "abc.project.json");

                // Clean-up
            }
        }

        [Fact]
        public void BuildIntegratedNuGetProject_GetLockFilePathWithNoFiles()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var expected = Path.Combine(randomProjectFolderPath, "project.json");

                // Act
                var path = BuildIntegratedProjectUtility.GetProjectConfigPath(randomProjectFolderPath, "abc");

                // Assert
                Assert.Equal(expected, path);

                // Clean-up
            }
        }

        [Fact]
        public void BuildIntegratedNuGetProject_GetLockFilePathWithProjectJsonOnly()
        {
            // Arrange
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projJsonFile = Path.Combine(randomProjectFolderPath, "project.json");
                CreateFile(projJsonFile);

                // Act
                var path = BuildIntegratedProjectUtility.GetProjectConfigPath(randomProjectFolderPath, "abc");
                var fileName = Path.GetFileName(path);

                // Assert
                Assert.Equal(fileName, "project.json");

                // Clean-up
            }
        }

        [Theory]
        [InlineData("abc", "abc.project.json")]
        [InlineData("ABC", "ABC.project.json")]
        [InlineData("A B C", "A B C.project.json")]
        [InlineData("a.b.c", "a.b.c.project.json")]
        [InlineData(" ", " .project.json")]
        public void BuildIntegratedNuGetProject_GetProjectConfigWithProjectName(string projectName, string fileName)
        {
            // Arrange & Act
            var generatedName = BuildIntegratedProjectUtility.GetProjectConfigWithProjectName(projectName);

            // Assert
            Assert.Equal(fileName, generatedName);
        }

        [Theory]
        [InlineData("abc", "abc.project.lock.json")]
        [InlineData("ABC", "ABC.project.lock.json")]
        [InlineData("A B C", "A B C.project.lock.json")]
        [InlineData("a.b.c", "a.b.c.project.lock.json")]
        [InlineData(" ", " .project.lock.json")]
        public void BuildIntegratedNuGetProject_GetProjectLockFileNameWithProjectName(
            string projectName,
            string fileName)
        {
            // Arrange & Act
            var generatedName = BuildIntegratedProjectUtility.GetProjectLockFileNameWithProjectName(projectName);

            // Assert
            Assert.Equal(fileName, generatedName);
        }

        [Theory]
        [InlineData("abc", "abc.project.json")]
        [InlineData("ABC", "ABC.project.json")]
        [InlineData("A B C", "A B C.project.json")]
        [InlineData("a.b.c", "a.b.c.project.json")]
        [InlineData(" ", " .project.json")]
        [InlineData("", ".project.json")]
        public void BuildIntegratedNuGetProject_GetProjectNameFromConfigFileName(
            string projectName,
            string fileName)
        {
            // Arrange & Act
            var result = BuildIntegratedProjectUtility.GetProjectNameFromConfigFileName(fileName);

            // Assert
            Assert.Equal(projectName, result);
        }

        [Theory]
        [InlineData("abc.project.json")]
        [InlineData("a b c.project.json")]
        [InlineData("MY LONG PROJECT NAME 234234432.project.json")]
        [InlineData("packages.config.project.json")]
        [InlineData("111.project.json")]
        [InlineData("project.json")]
        [InlineData("prOject.JSon")]
        [InlineData("xyz.prOject.JSon")]
        [InlineData("c:\\users\\project.json")]
        [InlineData("dir\\project.json")]
        [InlineData("c:\\users\\abc.project.json")]
        [InlineData("dir\\abc.project.json")]
        [InlineData(".\\abc.project.json")]
        public void BuildIntegratedNuGetProject_IsProjectConfig_True(string path)
        {
            // Arrange & Act
            var result = BuildIntegratedProjectUtility.IsProjectConfig(path);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("abcproject.json")]
        [InlineData("a b c.project.jso")]
        [InlineData("abc.project..json")]
        [InlineData("packages.config")]
        [InlineData("project.json ")]
        [InlineData("c:\\users\\packages.config")]
        [InlineData("c:\\users\\abc.project..json")]
        [InlineData("c:\\users\\")]
        [InlineData("<Shared>")]
        [InlineData("<Shared>.Project.json")]
        [InlineData("\t")]
        [InlineData("")]
        public void BuildIntegratedNuGetProject_IsProjectConfig_False(string path)
        {
            // Arrange & Act
            var result = BuildIntegratedProjectUtility.IsProjectConfig(path);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("project.json", "project.lock.json")]
        [InlineData("dir/project.json", "dir\\project.lock.json")]
        [InlineData("c:\\users\\project.json", "c:\\users\\project.lock.json")]
        [InlineData("abc.project.json", "abc.project.lock.json")]
        [InlineData("dir/abc.project.json", "dir\\abc.project.lock.json")]
        [InlineData("c:\\users\\abc.project.json", "c:\\users\\abc.project.lock.json")]
        public void BuildIntegratedNuGetProject_GetLockFilePath(string configPath, string lockFilePath)
        {
            // Arrange & Act
            var result = BuildIntegratedProjectUtility.GetLockFilePath(configPath);

            // Assert
            Assert.Equal(lockFilePath, result);
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
                var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

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
                var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

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
                var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

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
