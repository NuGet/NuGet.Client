// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement.Projects;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace ProjectManagement.Test
{
    public class BuildIntegratedNuGetProjectTests
    {
        [Fact]
        public async Task TestBuildIntegratedNuGetProjectInstallPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJson(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
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

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestBuildIntegratedNuGetProjectUninstallPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var packageIdentity2 = new PackageIdentity("packageB", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJson(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var packageFileInfo2 = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
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

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestBuildIntegratedNuGetProjectUninstallAllPackages()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var packageIdentity2 = new PackageIdentity("packageB", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJson(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var packageFileInfo2 = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
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

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        private static void CreateConfigJson(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.Write(BasicConfig.ToString());
            }
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
    }
}
