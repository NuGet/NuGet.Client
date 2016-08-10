// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.CommandLine.XPlat;
using NuGet.Commands;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatRestoreTests
    {
        [Theory]
        // Try with config file in the project directory
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.Klondike)]
        [InlineData(TestServers.MyGet)]
        [InlineData(TestServers.Nexus)]
        [InlineData(TestServers.NuGetServer)]
        [InlineData(TestServers.ProGet)]
        public void Restore_WithConfigFileInProjectDirectory_Succeeds(string sourceUri)
        {
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var configFile = XPlatTestUtils.CopyFuncTestConfig(projectDir);

                var specPath = Path.Combine(projectDir, "XPlatRestoreTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "costura.fody", "1.3.3");
                XPlatTestUtils.AddDependency(spec, "fody", "1.29.4");
                XPlatTestUtils.WriteJson(spec, specPath);

                var log = new TestCommandOutputLogger();

                var args = new List<string>()
                {
                    "restore",
                    projectDir,
                    "--packages",
                    packagesDir,
                    "--source",
                    sourceUri,
                    "--no-cache"
                };

                // Act
                int exitCode = Program.MainInternal(args.ToArray(), log);

                Assert.Contains($@"OK {sourceUri}/FindPackagesById()?id='fody'", log.ShowMessages());
                Assert.Equal(string.Empty, log.ShowErrors());
                Assert.Equal(0, exitCode);

                var lockFilePath = Path.Combine(projectDir, "XPlatRestoreTests", "project.lock.json");
                Assert.True(File.Exists(lockFilePath));
            }
        }

        [Theory]
        // Try with config file in a different directory
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.Klondike)]
        [InlineData(TestServers.MyGet)]
        [InlineData(TestServers.Nexus)]
        [InlineData(TestServers.NuGetServer)]
        [InlineData(TestServers.ProGet)]
        public void Restore_WithConfigFileInDifferentDirectory_Succeeds(string sourceUri)
        {
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var configDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var configFile = XPlatTestUtils.CopyFuncTestConfig(configDir);

                var specPath = Path.Combine(projectDir, "XPlatRestoreTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "costura.fody", "1.3.3");
                XPlatTestUtils.AddDependency(spec, "fody", "1.29.4");
                XPlatTestUtils.WriteJson(spec, specPath);

                var log = new TestCommandOutputLogger();

                var args = new List<string>()
                {
                    "restore",
                    projectDir,
                    "--packages",
                    packagesDir,
                    "--source",
                    sourceUri,
                    "--no-cache",
                    "--configfile",
                    configFile
                };

                // Act
                int exitCode = Program.MainInternal(args.ToArray(), log);

                Assert.Contains($@"OK {sourceUri}/FindPackagesById()?id='fody'", log.ShowMessages());
                Assert.Equal(string.Empty, log.ShowErrors());
                Assert.Equal(0, exitCode);

                var lockFilePath = Path.Combine(projectDir, "XPlatRestoreTests", "project.lock.json");
                Assert.True(File.Exists(lockFilePath));
            }
        }

        [Fact]
        public void Restore_WithMissingConfigFile_Fails()
        {
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var configDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "XPlatRestoreTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "costura.fody", "1.3.3");
                XPlatTestUtils.AddDependency(spec, "fody", "1.29.4");
                XPlatTestUtils.WriteJson(spec, specPath);

                var log = new TestCommandOutputLogger();

                var args = new string[]
                {
                    "restore",
                    projectDir,
                    "--configfile",
                    Path.Combine(configDir, "DoesNotExist.config"),
                };

                // Act
                var exitCode = Program.MainInternal(args, log);

                // Assert
                Assert.Equal(1, exitCode);
                Assert.Equal(1, log.Errors);
                Assert.Contains("DoesNotExist.config", log.ShowErrors()); // file does not exist
            }
        }

        [Fact]
        public async Task Restore_FallbackFolderContainsAllPackages()
        {
            using (var sourceDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var fallbackDir1 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var fallbackDir2 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "XPlatRestoreTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "a", "1.0.0");
                XPlatTestUtils.AddDependency(spec, "b", "1.0.0");
                XPlatTestUtils.WriteJson(spec, specPath);

                var packageA = new SimpleTestPackageContext("a", "1.0.0");
                var packageB = new SimpleTestPackageContext("b", "1.0.0");

                var saveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files;

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    fallbackDir2,
                    saveMode,
                    packageA,
                    packageB);

                var log = new TestCommandOutputLogger();

                var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <config>
        <add key=""globalPackagesFolder"" value=""{packagesDir}"" />
    </config>
    <fallbackPackageFolders>
        <add key=""a"" value=""{fallbackDir1}"" />
        <add key=""b"" value=""{fallbackDir2}"" />
    </fallbackPackageFolders>
    <packageSources>
        <add key=""a"" value=""{sourceDir}"" />
    </packageSources>
</configuration>";

                File.WriteAllText(Path.Combine(projectDir, "NuGet.Config"), config);

                var args = new string[]
                {
                    "restore",
                    projectDir,
                };

                // Act
                var exitCode = Program.MainInternal(args, log);

                // Assert
                Assert.Equal(0, exitCode);
                Assert.Equal(0, log.Errors);
                Assert.Equal(0, log.Warnings);
                Assert.Equal(0, Directory.GetDirectories(packagesDir).Length);
            }
        }

        [Fact]
        public async Task Restore_FallbackFolderContainsUnrelatedPackages()
        {
            using (var fallbackDir1 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var fallbackDir2 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var sourceDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "XPlatRestoreTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "x", "1.0.0");
                XPlatTestUtils.AddDependency(spec, "y", "1.0.0");
                XPlatTestUtils.WriteJson(spec, specPath);

                var packageA = new SimpleTestPackageContext("a", "1.0.0");
                var packageB = new SimpleTestPackageContext("b", "1.0.0");
                var packageX = new SimpleTestPackageContext("x", "1.0.0");
                var packageY = new SimpleTestPackageContext("y", "1.0.0");

                var saveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files;

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    fallbackDir1,
                    saveMode,
                    packageB);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    fallbackDir2,
                    saveMode,
                    packageB);

                SimpleTestPackageUtility.CreatePackages(sourceDir, packageX, packageY);

                var log = new TestCommandOutputLogger();

                var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <config>
        <add key=""globalPackagesFolder"" value=""{packagesDir}"" />
    </config>
    <fallbackPackageFolders>
        <add key=""a"" value=""{fallbackDir1}"" />
        <add key=""b"" value=""{fallbackDir2}"" />
    </fallbackPackageFolders>
    <packageSources>
        <add key=""a"" value=""{sourceDir}"" />
    </packageSources>
</configuration>";

                File.WriteAllText(Path.Combine(projectDir, "NuGet.Config"), config);

                var args = new string[]
                {
                    "restore",
                    projectDir,
                };

                // Act
                var exitCode = Program.MainInternal(args, log);

                // Assert
                Assert.Equal(0, exitCode);
                Assert.Equal(0, log.Errors);
                Assert.Equal(0, log.Warnings);
                Assert.Equal(2, Directory.GetDirectories(packagesDir).Length);
            }
        }

        [Fact]
        public async Task Restore_FallbackFolderDoesNotExist()
        {
            using (var sourceDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var fallbackDir1 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var fallbackDir2 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "XPlatRestoreTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "a", "1.0.0");
                XPlatTestUtils.AddDependency(spec, "b", "1.0.0");
                XPlatTestUtils.WriteJson(spec, specPath);

                var packageA = new SimpleTestPackageContext("a", "1.0.0");
                var packageB = new SimpleTestPackageContext("b", "1.0.0");

                var saveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files;

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    fallbackDir2,
                    saveMode,
                    packageA,
                    packageB);

                Directory.Delete(fallbackDir1);

                var log = new TestCommandOutputLogger();

                var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <config>
        <add key=""globalPackagesFolder"" value=""{packagesDir}"" />
    </config>
    <fallbackPackageFolders>
        <add key=""a"" value=""{fallbackDir1}"" />
        <add key=""b"" value=""{fallbackDir2}"" />
    </fallbackPackageFolders>
    <packageSources>
        <add key=""a"" value=""{sourceDir}"" />
    </packageSources>
</configuration>";

                File.WriteAllText(Path.Combine(projectDir, "NuGet.Config"), config);

                var args = new string[]
                {
                    "restore",
                    projectDir,
                };

                // Act
                var exitCode = Program.MainInternal(args, log);

                // Assert
                Assert.Equal(1, exitCode);
                Assert.Equal(1, log.Errors);
                Assert.Contains(fallbackDir1, log.ShowErrors());
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Restore_RespectsLegacyPackagesDirectorySwitch(bool useSwitch)
        {
            using (var sourceDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "XPlatRestoreTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                var packageAId = "PackageA";
                var packageBId = "PackageB";
                var packageAVersion = "1.0.0-Beta";
                var packageBVersion = "2.0.0-Beta";

                XPlatTestUtils.AddDependency(spec, packageAId, packageAVersion);
                XPlatTestUtils.AddDependency(spec, packageBId, packageBVersion);
                XPlatTestUtils.WriteJson(spec, specPath);

                var packageA = new SimpleTestPackageContext(packageAId, packageAVersion);
                var packageB = new SimpleTestPackageContext(packageBId, packageBVersion);
                
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    sourceDir,
                    PackageSaveMode.Defaultv3,
                    packageA,
                    packageB);

                var log = new TestCommandOutputLogger();

                var config = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <config>
        <add key=""globalPackagesFolder"" value=""{packagesDir}"" />
    </config>
    <packageSources>
        <add key=""a"" value=""{sourceDir}"" />
    </packageSources>
</configuration>";

                File.WriteAllText(Path.Combine(projectDir, "NuGet.Config"), config);

                var args = new List<string>
                {
                    "restore",
                    projectDir
                };

                if (useSwitch)
                {
                    args.Add("--legacy-packages-directory");
                }

                // Act
                var exitCode = Program.MainInternal(args.ToArray(), log);

                // Assert
                Assert.Equal(0, exitCode);
                Assert.Equal(0, log.Errors);
                Assert.Equal(0, log.Warnings);

                var resolver = new VersionFolderPathResolver(packagesDir, isLowercase: !useSwitch);
                
                Assert.True(File.Exists(resolver.GetPackageFilePath(packageAId, NuGetVersion.Parse(packageAVersion))));
                Assert.True(File.Exists(resolver.GetPackageFilePath(packageBId, NuGetVersion.Parse(packageBVersion))));

                // The switch results in both cases being restored.
                if (useSwitch)
                {
                    var lowercaseResolver = new VersionFolderPathResolver(packagesDir, isLowercase: true);
                    Assert.True(File.Exists(lowercaseResolver.GetPackageFilePath(packageAId, NuGetVersion.Parse(packageAVersion))));
                    Assert.True(File.Exists(lowercaseResolver.GetPackageFilePath(packageBId, NuGetVersion.Parse(packageBVersion))));
                }

                // Verify the lock file has the correct path.
                var lockFileFormat = new LockFileFormat();
                var lockFilePath = Path.ChangeExtension(specPath, "lock.json");
                var lockFile = lockFileFormat.Read(lockFilePath);

                var packageALibrary = lockFile.Libraries.FirstOrDefault(l => l.Name == packageAId);
                Assert.NotNull(packageALibrary);
                Assert.Equal(packageAVersion, packageALibrary.Version.ToFullString());
                Assert.Equal(LibraryType.Package, packageALibrary.Type);
                Assert.Equal(
                    PathUtility.GetPathWithForwardSlashes(resolver.GetPackageDirectory(packageAId, NuGetVersion.Parse(packageAVersion))),
                    packageALibrary.Path);

                var packageBLibrary = lockFile.Libraries.FirstOrDefault(l => l.Name == packageBId);
                Assert.NotNull(packageBLibrary);
                Assert.Equal(packageBVersion, packageBLibrary.Version.ToFullString());
                Assert.Equal(LibraryType.Package, packageBLibrary.Type);
                Assert.Equal(
                    PathUtility.GetPathWithForwardSlashes(resolver.GetPackageDirectory(packageBId, NuGetVersion.Parse(packageBVersion))),
                    packageBLibrary.Path);
            }
        }
    }
}
