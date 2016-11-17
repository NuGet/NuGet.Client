// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetInstallCommandTest
    {
        [Fact]
        public void InstallCommand_FromPackagesConfigFileWithExcludeVersion()
        {
            // Arrange
            using (var workingPath = TestDirectory.Create())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var nugetexe = Util.GetNuGetExePath();

                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                Directory.CreateDirectory(repositoryPath);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
                Util.CreateFile(workingPath, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    $"install -OutputDirectory outputDir -Source {repositoryPath} -ExcludeVersion",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageADir = Path.Combine(workingPath, "outputDir", "packageA");
                var packageBDir = Path.Combine(workingPath, "outputDir", "packageB");
                Assert.True(Directory.Exists(packageADir));
                Assert.True(Directory.Exists(packageBDir));
            }
        }

        [Fact]
        public async Task InstallCommand_WithExcludeVersion()
        {
            using (var source = TestDirectory.Create())
            using (var outputDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);

                // Act
                string[] args = new string[] {
                    "install", "testPackage1",
                    "-OutputDirectory", outputDirectory,
                    "-Source", source,
                    "-ExcludeVersion" };

                int r = await Task.Run(() => Program.Main(args));

                // Assert
                Assert.Equal(0, r);

                var packageDir = Path.Combine(
                    outputDirectory,
                    @"testPackage1");

                Assert.True(Directory.Exists(packageDir));
            }
        }

        [Fact]
        public void InstallCommand_FromPackagesConfigFile()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");

                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                Directory.CreateDirectory(repositoryPath);
                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
                Util.CreateFile(workingPath, "packages.config",
    @"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                string[] args = new string[]
                {
                    "install",
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    repositoryPath
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, "outputDir", "packageA.1.1.0", "packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, "outputDir", "packageB.2.2.0", "packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Fact]
        public void InstallCommand_ShowsAlreadyInstalledMessageWhenAllPackagesArePresent()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var packagesConfig = Path.Combine(workingPath, "packages.config");

                Directory.CreateDirectory(repositoryPath);
                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
                Util.CreateFile(workingPath, "packages.config",
    @"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                string[] args = new string[]
                {
                    "install",
                    packagesConfig,
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    repositoryPath
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, "outputDir", "packageA.1.1.0", "packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, "outputDir", "packageB.2.2.0", "packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));

                //Act (Install a second time)
                string[] args2 = new string[]
                {
                    "install",
                    packagesConfig,
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    repositoryPath
                };

                var r1 = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args2),
                    waitForExit: true);

                //Assert
                var message = r1.Item2;
                string alreadyInstalledMessage = String.Format("All packages listed in {0} are already installed.", packagesConfig);
                Assert.Contains(alreadyInstalledMessage, message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void InstallCommand_FromPackagesConfigFile_SpecifyingSolutionDir()
        {
            // Arrange
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            {
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");

                Directory.CreateDirectory(repositoryPath);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
                Util.CreateFile(workingPath, "packages.config",
    @"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                string[] args = new string[]
                {
                    "install",
                    "-SolutionDir",
                    $"\"{workingPath}\"",
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    $"\"{repositoryPath}\""
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, $"{r.Item2} {r.Item3}");
                var packageFileA = Path.Combine(workingPath, "outputDir", "packageA.1.1.0", "packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, "outputDir", "packageB.2.2.0", "packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Fact]
        public void InstallCommand_FromPackagesConfigFile_SpecifyingRelativeSolutionDir()
        {
            // Arrange
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            {
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                string folderName = Path.GetFileName(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var relativeFolderPath = $"..\\{folderName}";

                Directory.CreateDirectory(repositoryPath);
                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
                Util.CreateFile(workingPath, "packages.config",
    @"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                string[] args = new string[]
                {
                    "install",
                    "-SolutionDir",
                    relativeFolderPath,
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    repositoryPath };

                // Act
                var path = Environment.GetEnvironmentVariable("PATH");
                Environment.SetEnvironmentVariable("PATH", null);
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);
                Environment.SetEnvironmentVariable("PATH", path);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, "outputDir", "packageA.1.1.0", "packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, "outputDir", "packageB.2.2.0", "packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        public void InstallCommand_PackageSaveModeNuspec()
        {
            using (var source = TestDirectory.Create())
            using (var outputDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);

                // Act
                string[] args = new string[] {
                    "install", "testPackage1",
                    "-OutputDirectory", outputDirectory,
                    "-Source", source,
                    "-PackageSaveMode", "nuspec" };
                int r = Program.Main(args);

                // Assert
                Assert.Equal(0, r);

                var nuspecFile = Path.Combine(
                    outputDirectory,
                    "testPackage1.1.1.0", "testPackage1.1.1.0.nuspec");

                Assert.True(File.Exists(nuspecFile));
                var nupkgFiles = Directory.GetFiles(outputDirectory, "*.nupkg", SearchOption.AllDirectories);
                Assert.Equal(0, nupkgFiles.Length);
            }
        }

        public void InstallCommand_PackageSaveModeNupkg()
        {
            using (var source = TestDirectory.Create())
            using (var outputDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);

                // Act
                string[] args = new string[] {
                    "install", "testPackage1",
                    "-OutputDirectory", outputDirectory,
                    "-Source", source,
                    "-PackageSaveMode", "nupkg" };
                int r = Program.Main(args);

                // Assert
                Assert.Equal(0, r);

                var nupkgFile = Path.Combine(
                    outputDirectory,
                    "testPackage1.1.1.0", "testPackage1.1.1.0.nuspec");

                Assert.True(File.Exists(nupkgFile));
                var nuspecFiles = Directory.GetFiles(outputDirectory, "*.nuspec", SearchOption.AllDirectories);
                Assert.Equal(0, nuspecFiles.Length);
            }
        }

        public void InstallCommand_PackageSaveModeNuspecNupkg()
        {
            using (var source = TestDirectory.Create())
            using (var outputDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);

                // Act
                string[] args = new string[] {
                    "install", "testPackage1",
                    "-OutputDirectory", outputDirectory,
                    "-Source", source,
                    "-PackageSaveMode", "nupkg;nuspec" };
                int r = Program.Main(args);

                // Assert
                Assert.Equal(0, r);

                var nupkgFile = Path.Combine(
                    outputDirectory,
                    "testPackage1.1.1.0", "testPackage1.1.1.0.nuspec");
                var nuspecFile = Path.ChangeExtension(nupkgFile, "nuspec");

                Assert.True(File.Exists(nupkgFile));
                Assert.True(File.Exists(nuspecFile));
            }
        }

        // Test that after a package is installed with -PackageSaveMode nuspec, nuget.exe
        // can detect that the package is already installed when trying to install the same
        // package.
        public void InstallCommand_PackageSaveModeNuspecReinstall()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestDirectory.Create())
            using (var outputDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);

                string[] args = new string[] {
                    "install", "testPackage1",
                    "-OutputDirectory", outputDirectory,
                    "-Source", source,
                    "-PackageSaveMode", "nuspec" };
                int r = Program.Main(args);
                Assert.Equal(0, r);

                // Act
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                var output = result.Item2;

                // Assert
                var expectedOutput = "'testPackage1 1.1.0' already installed." +
                    Environment.NewLine;
                Assert.Equal(expectedOutput, output);
            }
        }

        // Test that PackageSaveMode specified in nuget.config file is used.
        public void InstallCommand_PackageSaveModeInConfigFile()
        {
            using (var source = TestDirectory.Create())
            using (var outputDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage(
                    "testPackage1", "1.1.0", source);

                var configFile = Path.Combine(source, "nuget.config");
                Util.CreateFile(Path.GetDirectoryName(configFile), Path.GetFileName(configFile), "<configuration/>");
                string[] args = new string[] {
                    "config", "-Set", "PackageSaveMode=nuspec",
                    "-ConfigFile", configFile };
                int r = Program.Main(args);
                Assert.Equal(0, r);

                // Act
                args = new string[] {
                    "install", "testPackage1",
                    "-OutputDirectory", outputDirectory,
                    "-Source", source,
                    "-ConfigFile", configFile };
                r = Program.Main(args);

                // Assert
                Assert.Equal(0, r);

                var nuspecFile = Path.Combine(
                    outputDirectory,
                    "testPackage1.1.1.0", "testPackage1.1.1.0.nuspec");

                Assert.True(File.Exists(nuspecFile));
                var nupkgFiles = Directory.GetFiles(outputDirectory, "*.nupkg", SearchOption.AllDirectories);
                Assert.Equal(0, nupkgFiles.Length);
            }
        }

        // Tests that when package restore is enabled and -RequireConsent is specified,
        // the opt out message is displayed.
        [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.proj1.config")]
        public void InstallCommand_OptOutMessage(string configFileName)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            {
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(workingPath, "my.config",
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageRestore>
    <add key=""enabled"" value=""True"" />
  </packageRestore>
</configuration>");

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, configFileName,
    @"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");
                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "install " + configFileName + " -Source " + repositoryPath + $@" -ConfigFile ..{Path.AltDirectorySeparatorChar}my.config -RequireConsent -Verbosity detailed",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                string optOutMessage = String.Format(
                    CultureInfo.CurrentCulture,
                    NuGet.CommandLine.NuGetResources.RestoreCommandPackageRestoreOptOutMessage,
                    NuGet.Resources.NuGetResources.PackageRestoreConsentCheckBoxText.Replace("&", ""));
                Assert.Contains(optOutMessage.Replace("\r\n", "\n"), r.Item2.Replace("\r\n", "\n"));
            }
        }

        // Tests that when package restore is enabled, but -RequireConsent is not specified,
        // the opt out message is not displayed.
        [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.proj1.config")]
        public void InstallCommand_NoOptOutMessage(string configFileName)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            {
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(workingPath, "my.config",
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageRestore>
    <add key=""enabled"" value=""True"" />
  </packageRestore>
</configuration>");

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, configFileName,
    @"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");
                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "install " + configFileName + " -Source " + repositoryPath + $@" -ConfigFile ..{Path.AltDirectorySeparatorChar}my.config",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                string optOutMessage = String.Format(
                    CultureInfo.CurrentCulture,
                    NuGetResources.RestoreCommandPackageRestoreOptOutMessage,
                    NuGet.Resources.NuGetResources.PackageRestoreConsentCheckBoxText.Replace("&", ""));
                Assert.DoesNotContain(optOutMessage, r.Item2);
            }
        }

        // Tests that when no version is specified, nuget will query the server to get
        // the latest version number first.
        [Fact]
        public void InstallCommand_GetLastestReleaseVersion()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            using (var packageDirectory = TestDirectory.Create())
            {
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);

                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package1 = new ZipPackage(packageFileName);
                packageFileName = Util.CreateTestPackage("testPackage1", "1.2.0", packageDirectory);
                var package2 = new ZipPackage(packageFileName);

                using (var server = Util.CreateMockServer(new[] { package1, package2 }))
                {
                    server.Start();

                    // Act
                    var args = "install testPackage1 -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingPath,
                        args,
                        waitForExit: true);

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // testPackage1 1.2.0 is installed
                    Assert.True(Directory.Exists(Path.Combine(workingPath, "packages", "testPackage1.1.2.0")));
                }
            }
        }

        // Tests that when no version is specified, and -Prerelease is specified,
        // nuget will query the server to get the latest prerelease version number first.
        [Fact]
        public void InstallCommand_GetLastestPrereleaseVersion()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            using (var packageDirectory = TestDirectory.Create())
            {
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package1 = new ZipPackage(packageFileName);

                packageFileName = Util.CreateTestPackage("testPackage1", "1.2.0-beta1", packageDirectory);
                var package2 = new ZipPackage(packageFileName);

                using (var server = Util.CreateMockServer(new[] { package1, package2 }))
                {
                    server.Start();

                    // Act
                    var args = "install testPackage1 -Prerelease -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingPath,
                        args,
                        waitForExit: true);

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // testPackage1 1.2.0-beta1 is installed
                    Assert.True(Directory.Exists(Path.Combine(workingPath, "packages", "testPackage1.1.2.0-beta1")));
                }
            }
        }

        // Tests that when prerelease version is specified, and -Prerelease is not specified,
        [Fact]
        public void InstallCommand_WithPrereleaseVersionSpecified()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            using (var packageDirectory = TestDirectory.Create())
            {
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package1 = new ZipPackage(packageFileName);

                packageFileName = Util.CreateTestPackage("testPackage1", "1.2.0-beta1", packageDirectory);
                var package2 = new ZipPackage(packageFileName);

                using (var server = Util.CreateMockServer(new[] { package1, package2 }))
                {
                    server.Start();

                    // Act
                    var args = "install testPackage1 -Version 1.2.0-beta1 -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingPath,
                        args,
                        waitForExit: true);

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // testPackage1 1.2.0-beta1 is installed
                    Assert.True(Directory.Exists(Path.Combine(workingPath, "packages", "testPackage1.1.2.0-beta1")));
                }
            }
        }

        // Tests that when -Version is specified, nuget will use request
        // Packages(Id='id',Version='version') to get the specified version
        [Fact]
        public void InstallCommand_WithVersionSpecified()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            using (var packageDirectory = TestDirectory.Create())
            {
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);

                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                using (var server = new MockServer())
                {
                    bool getPackageByVersionIsCalled = false;
                    bool packageDownloadIsCalled = false;

                    server.Get.Add("/nuget/$metadata", r =>
                       MockServerResource.NuGetV2APIMetadata);
                    server.Get.Add("/nuget/Packages(Id='testPackage1',Version='1.1.0')", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            getPackageByVersionIsCalled = true;
                            response.ContentType = "application/atom+xml;type=entry;charset=utf-8";
                            var p1 = server.ToOData(package);
                            MockServer.SetResponseContent(response, p1);
                        }));

                    server.Get.Add("/package/testPackage1", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            packageDownloadIsCalled = true;
                            response.ContentType = "application/zip";
                            using (var stream = package.GetStream())
                            {
                                var content = stream.ReadAllBytes();
                                MockServer.SetResponseContent(response, content);
                            }
                        }));

                    server.Get.Add("/nuget", r => "OK");

                    server.Start();

                    // Act
                    var args = "install testPackage1 -Version 1.1.0 -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingPath,
                        args,
                        waitForExit: true);

                    // Assert
                    Assert.Equal(0, r1.Item1);
                    Assert.True(getPackageByVersionIsCalled);
                    Assert.True(packageDownloadIsCalled);
                }
            }
        }

        // Tests that when -Version is specified, if the specified version cannot be found,
        // nuget will retry with new version numbers by appending 0's to the specified version.
        [Fact(Skip = "Exact packages are no longer requested")]
        public void InstallCommand_WillTryNewVersionsByAppendingZeros()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            {
                // Arrange
                using (var server = new MockServer())
                {
                    List<string> requests = new List<string>();
                    server.Get.Add("/nuget/$metadata", r =>
                       MockServerResource.NuGetV2APIMetadata);
                    server.Get.Add("/nuget/Packages", r =>
                    {
                        requests.Add(r.Url.ToString());
                        return HttpStatusCode.NotFound;
                    });
                    server.Get.Add("/nuget", r => "OK");

                    server.Start();

                    // Act
                    var args = "install testPackage1 -Version 1.1 -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingPath,
                        args,
                        waitForExit: true);

                    // Assert
                    Assert.True(1 == r1.Item1, r1.Item2 + " " + r1.Item3);

                    Assert.Equal(3, requests.Count);
                    Assert.True(requests[0].EndsWith("Packages(Id='testPackage1',Version='1.1')"));
                    Assert.True(requests[1].EndsWith("Packages(Id='testPackage1',Version='1.1.0')"));
                    Assert.True(requests[2].EndsWith("Packages(Id='testPackage1',Version='1.1.0.0')"));
                }
            }
        }

        // Tests that nuget will NOT download package from http source if the package on the server
        // has the same hash value as the cached version.
        [Fact(Skip = "Failing on CI Build need to investigate the reason")]
        public void InstallCommand_WillUseCachedFile()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            using (var packageDirectory = TestDirectory.Create())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");

                // Arrange

                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);

                // add the package to machine cache
                MachineCache.Default.AddPackage(package);

                using (var server = new MockServer())
                {
                    string findPackagesByIdRequest = string.Empty;
                    bool packageDownloadIsCalled = false;

                    server.Get.Add("/nuget/$metadata", r =>
                       MockServerResource.NuGetV2APIMetadata);
                    server.Get.Add("/nuget/FindPackagesById()", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            findPackagesByIdRequest = r.Url.ToString();
                            response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                            string feed = server.ToODataFeed(new[] { package }, "FindPackagesById");
                            MockServer.SetResponseContent(response, feed);
                        }));

                    server.Get.Add("/nuget/Packages(Id='testPackage1',Version='1.1.0')", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            response.ContentType = "application/atom+xml;type=entry;charset=utf-8";
                            var p1 = server.ToOData(package);
                            MockServer.SetResponseContent(response, p1);
                        }));

                    server.Get.Add("/package/testPackage1", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            packageDownloadIsCalled = true;
                            response.ContentType = "application/zip";
                            using (var stream = package.GetStream())
                            {
                                var content = stream.ReadAllBytes();
                                MockServer.SetResponseContent(response, content);
                            }
                        }));

                    server.Get.Add("/nuget", r => "OK");

                    server.Start();

                    // Act
                    var args = "install testPackage1 -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingPath,
                        args,
                        waitForExit: true);

                    // Assert
                    Assert.True(0 == r1.Item1, r1.Item2 + " " + r1.Item3);

                    // verifies that package is NOT downloaded from server since nuget uses
                    // the file in machine cache.
                    Assert.False(packageDownloadIsCalled);
                }
            }
        }

        // Tests that nuget will download package from http source if the package on the server
        // has a different hash value from the cached version.
        [Fact(Skip = "Hashes are no longer checked on download")]
        public void InstallCommand_DownloadPackageWhenHashChanges()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestDirectory.Create())
            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");

                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);
                MachineCache.Default.RemovePackage(package);

                // add the package to machine cache
                MachineCache.Default.AddPackage(package);

                // create a new package. Now this package has different hash value from the package in
                // the machine cache.
                packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                package = new ZipPackage(packageFileName);

                using (var server = new MockServer())
                {
                    string findPackagesByIdRequest = string.Empty;
                    bool packageDownloadIsCalled = false;

                    server.Get.Add("/nuget/$metadata", r =>
                       MockServerResource.NuGetV2APIMetadata);
                    server.Get.Add("/nuget/FindPackagesById()", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            findPackagesByIdRequest = r.Url.ToString();
                            response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                            string feed = server.ToODataFeed(new[] { package }, "FindPackagesById");
                            MockServer.SetResponseContent(response, feed);
                        }));

                    server.Get.Add("/nuget/Packages(Id='testPackage1',Version='1.1.0')", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            response.ContentType = "application/atom+xml;type=entry;charset=utf-8";
                            var p1 = server.ToOData(package);
                            MockServer.SetResponseContent(response, p1);
                        }));

                    server.Get.Add("/package/testPackage1", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            packageDownloadIsCalled = true;
                            response.ContentType = "application/zip";
                            using (var stream = package.GetStream())
                            {
                                var content = stream.ReadAllBytes();
                                MockServer.SetResponseContent(response, content);
                            }
                        }));

                    server.Get.Add("/nuget", r => "OK");

                    server.Start();

                    // Act
                    var args = "install testPackage1 -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingPath,
                        args,
                        waitForExit: true);

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // verifies that package is downloaded from server since the cached version has
                    // a different hash from the package on the server.
                    Assert.True(packageDownloadIsCalled);
                }
            }
        }

        // Tests that when both the normal package and the symbol package exist in a local repository,
        // nuget install should pick the normal package.
        [Fact]
        public void InstallCommand_PreferNonSymbolPackage()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestDirectory.Create())
            using (var outputDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);
                var symbolPackageFileName = PackageCreater.CreateSymbolPackage(
                    "testPackage1", "1.1.0", source);

                // Act
                string[] args = new string[] {
                    "install", "testPackage1",
                    "-OutputDirectory", outputDirectory,
                    "-Source", source };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var testTxtFile = Path.Combine(
                    outputDirectory,
                    "testPackage1.1.1.0", "content", "test1.txt");
                Assert.True(File.Exists(testTxtFile));

                var symbolTxtFile = Path.Combine(
                    outputDirectory,
                    "testPackage1.1.1.0", "symbol.txt");
                Assert.False(File.Exists(symbolTxtFile));
            }
        }

        [Fact]
        public void InstallCommand_DependencyResolutionFailure()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestDirectory.Create())
            using (var outputDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source,
                    (builder) =>
                    {
                        var dependencySet = new PackageDependencySet(null,
                            new[] {
                                new PackageDependency(
                                    "non_existing",
                                    VersionUtility.ParseVersionSpec("1.1"))
                            });
                        builder.DependencySets.Add(dependencySet);
                    });

                // Act
                var args = String.Format(
                    CultureInfo.InvariantCulture,
                    "install testPackage1 -OutputDirectory {0} -Source {1}", outputDirectory, source);
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    args,
                    waitForExit: true);

                // Assert
                Assert.NotEqual(0, r.Item1);
                Assert.Contains("Unable to resolve dependency 'non_existing'", r.Item3);
            }
        }

        // Tests that when credential is saved in the config file, it will be passed
        // correctly to both the index.json endpoint and registration endpoint, even
        // though one uri does not start with the other uri.
        [Fact]
        public void InstallCommand_AuthenticatedV3WithCredentialSavedInConfig()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var randomTestFolder = TestDirectory.Create())
            {
                bool credentialsPassedToRegistrationEndPoint = false;

                // Server setup
                using (var serverV3 = new MockServer())
                {
                    var registrationEndPoint = serverV3.Uri + "w";
                    var indexJson = Util.CreateIndexJson();
                    Util.AddRegistrationResource(indexJson, serverV3);

                    serverV3.Get.Add("/a/b/c/index.json", r =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = String.IsNullOrEmpty(h) ?
                            null :
                            System.Text.Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));

                        if (StringComparer.OrdinalIgnoreCase.Equals("test_user:test_password", credential))
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = (int)HttpStatusCode.OK;
                                response.ContentType = "application/json";
                                MockServer.SetResponseContent(response, indexJson.ToString());
                            });
                        }
                        else
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.AddHeader("WWW-Authenticate", "Basic ");
                                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            });
                        }
                    });

                    serverV3.Get.Add("/reg/test_package/index.json", r =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = String.IsNullOrEmpty(h) ?
                            null :
                            System.Text.Encoding.Default.GetString(Convert.FromBase64String(h.Substring(6)));

                        if (StringComparer.OrdinalIgnoreCase.Equals("test_user:test_password", credential))
                        {
                            credentialsPassedToRegistrationEndPoint = true;

                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = (int)HttpStatusCode.OK;
                                response.ContentType = "text/javascript";
                                MockServer.SetResponseContent(response, indexJson.ToString());
                            });
                        }
                        else
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.AddHeader("WWW-Authenticate", "Basic ");
                                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            });
                        }
                    });

                    serverV3.Start();

                    // create the config file with credentials saved
                    var config = string.Format(
    @"<configuration>
  <packageSources>
    <add key='test' value='{0}' />
  </packageSources>
  <packageSourceCredentials>
    <test>
      <add key='UserName' value='test_user' />
      <add key='ClearTextPassword' value='test_password' />
    </test>
  </packageSourceCredentials>
</configuration>
",
    serverV3.Uri + "a/b/c/index.json");
                    var configFileName = Path.Combine(randomTestFolder, "nuget.config");
                    File.WriteAllText(configFileName, config);

                    // Act
                    string[] args = new string[]
                    {
                        "install test_package",
                        "-Source ",
                        serverV3.Uri + "a/b/c/index.json",
                        "-ConfigFile",
                        configFileName,
                        "-Verbosity detailed"
                    };
                    var result = CommandRunner.Run(
                        nugetexe,
                        Directory.GetCurrentDirectory(),
                        string.Join(" ", args),
                        true);

                    // Assert
                    Assert.True(credentialsPassedToRegistrationEndPoint);
                }
            }
        }

        [Fact]
        public void TestInstallWhenNoFeedAvailable()
        {
            var nugetexe = Util.GetNuGetExePath();
            using (var randomTestFolder = TestDirectory.Create())
            {
                // Create an empty config file and pass it as -ConfigFile switch.
                // This imitates the scenario where there is a machine without a default nuget.config under %APPDATA%
                // In this case, nuget will not create default nuget.config for user.
                var config = string.Format(
    @"<?xml version='1.0' encoding='utf - 8'?>
<configuration/>
");
                var configFileName = Path.Combine(randomTestFolder, "nuget.config");
                File.WriteAllText(configFileName, config);

                string[] args = new string[]
                {
                        "install Newtonsoft.Json",
                        "-version",
                        "7.0.1",
                        "-ConfigFile",
                        configFileName
                };

                var result = CommandRunner.Run(
                    nugetexe,
                    randomTestFolder,
                    string.Join(" ", args),
                    true);

                var expectedPath = Path.Combine(
                    randomTestFolder,
                    "Newtonsoft.Json.7.0.1",
                    "Newtonsoft.Json.7.0.1.nupkg");

                Assert.False(File.Exists(expectedPath), "nuget.exe installed Newtonsoft.Json.7.0.1");
            }
        }
    }
}