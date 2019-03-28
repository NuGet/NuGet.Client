// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetInstallCommandTest
    {
        [Fact]
        public async Task InstallCommand_PackageIdInstalledWithSxSAndExcludeVersionAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var packageA1 = new SimpleTestPackageContext("a", "1.0.0");

                var packageB1 = new SimpleTestPackageContext("b", "1.0.0");
                var packageB15 = new SimpleTestPackageContext("b", "1.5.0");
                var packageB2 = new SimpleTestPackageContext("b", "2.0.0");

                packageA1.Dependencies.Add(packageB2);

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageA1, packageB1, packageB2, packageB15);

                RunInstall(pathContext, "b", 0, "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot).Success.Should().BeTrue();
                RunInstall(pathContext, "b", 0, "-Version", "1.5.0", "-OutputDirectory", pathContext.SolutionRoot).Success.Should().BeTrue();
                RunInstall(pathContext, "a", 0, "-ExcludeVersion", "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot).Success.Should().BeTrue();

                Directory.GetDirectories(pathContext.SolutionRoot)
                    .Select(e => Path.GetFileName(e).ToLowerInvariant())
                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                    .Should()
                    .BeEquivalentTo(new[]
                    {
                        "a",
                        "b",
                        "b.1.0.0",
                        "b.1.5.0"
                    });
            }
        }

        [Fact]
        public async Task InstallCommand_PackageInstalledSxSWithOverlapOnDependencyAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var packageA1 = new SimpleTestPackageContext("a", "1.0.0");

                var packageB1 = new SimpleTestPackageContext("b", "1.0.0");
                var packageB2 = new SimpleTestPackageContext("b", "2.0.0");

                packageA1.Dependencies.Add(packageB2);

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageA1, packageB1, packageB2);

                RunInstall(pathContext, "b", 0, "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot).Success.Should().BeTrue();
                RunInstall(pathContext, "b", 0, "-Version", "2.0.0", "-OutputDirectory", pathContext.SolutionRoot).Success.Should().BeTrue();
                RunInstall(pathContext, "a", 0, "-ExcludeVersion", "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot).Success.Should().BeTrue();

                Directory.GetDirectories(pathContext.SolutionRoot)
                    .Select(e => Path.GetFileName(e).ToLowerInvariant())
                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                    .Should()
                    .BeEquivalentTo(new[]
                    {
                        "a",
                        "b.1.0.0",
                        "b.2.0.0"
                    });
            }
        }

        [Fact]
        public async Task InstallCommand_UpdatePackageWithExcludeVersionVerifyPackageReplacedAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageA1 = new SimpleTestPackageContext("a", "1.0.0");
                var packageA2 = new SimpleTestPackageContext("a", "2.0.0");

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageA1, packageA2);

                var pathResolver = new PackagePathResolver(pathContext.SolutionRoot, useSideBySidePaths: false);

                var r1 = RunInstall(pathContext, "a", 0, "-ExcludeVersion", "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot);

                // Act
                var r2 = RunInstall(pathContext, "a", 0, "-ExcludeVersion", "-Version", "2.0.0", "-OutputDirectory", pathContext.SolutionRoot);

                var nupkgPath = pathResolver.GetInstalledPackageFilePath(new PackageIdentity("a", NuGetVersion.Parse("2.0.0")));

                // Assert
                r1.Success.Should().BeTrue();
                r2.Success.Should().BeTrue();
                File.Exists(nupkgPath).Should().BeTrue();

                using (var reader = new PackageArchiveReader(nupkgPath))
                {
                    reader.NuspecReader.GetVersion().ToNormalizedString().Should().Be("2.0.0");
                }
            }
        }

        [Fact]
        public async Task InstallCommand_DependencyFailsToInstallVerifyFailureAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageA = new SimpleTestPackageContext("a", "1.0.0");
                var packageB = new SimpleTestPackageContext("b", "1.0.0");
                packageA.Dependencies.Add(packageB);

                // Only create A
                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageA);

                File.Delete(Directory.GetFiles(pathContext.PackageSource).Single(e => e.EndsWith("b.1.0.0.nupkg")));

                var pathResolver = new PackagePathResolver(pathContext.SolutionRoot, useSideBySidePaths: false);

                // Act
                var r1 = RunInstall(pathContext, "a", 1, "-ExcludeVersion", "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot, "-Source", pathContext.PackageSource);

                // Assert
                r1.Success.Should().BeFalse();
                r1.Errors.Should().Contain("Unable to resolve dependency");
            }
        }

        [Fact]
        public void InstallCommand_PackageFailsToInstallVerifyFailure()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Act
                var r1 = RunInstall(pathContext, "a", 1, "-ExcludeVersion", "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot, "-Source", pathContext.PackageSource);

                // Assert
                r1.Success.Should().BeFalse();
                r1.Errors.Should().Contain("Package 'a 1.0.0' is not found in the following");
            }
        }

        [Fact]
        public async Task InstallCommand_UpdatePackageWithExcludeVersionVerifyFilesRemovedAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageA1 = new SimpleTestPackageContext("a", "1.0.0");
                packageA1.AddFile("data/1.txt");

                var packageA2 = new SimpleTestPackageContext("a", "2.0.0");
                packageA2.AddFile("data/2.txt");

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageA1, packageA2);

                var pathResolver = new PackagePathResolver(pathContext.SolutionRoot, useSideBySidePaths: false);

                var r1 = RunInstall(pathContext, "a", 0, "-ExcludeVersion", "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot);

                // Act
                var r2 = RunInstall(pathContext, "a", 0, "-ExcludeVersion", "-Version", "2.0.0", "-OutputDirectory", pathContext.SolutionRoot);

                var nupkgPath = pathResolver.GetInstalledPackageFilePath(new PackageIdentity("a", NuGetVersion.Parse("2.0.0")));
                var installDir = Path.GetDirectoryName(nupkgPath);

                // Assert
                r1.Success.Should().BeTrue();
                r2.Success.Should().BeTrue();
                File.Exists(nupkgPath).Should().BeTrue();
                File.Exists(Path.Combine(installDir, "data", "1.txt")).Should().BeFalse("this package was uninstalled");
                File.Exists(Path.Combine(installDir, "data", "2.txt")).Should().BeTrue("this package was installed");

                using (var reader = new PackageArchiveReader(nupkgPath))
                {
                    reader.NuspecReader.GetVersion().ToNormalizedString().Should().Be("2.0.0");
                }
            }
        }

        [Fact]
        public async Task InstallCommand_DowngradePackageWithExcludeVersionVerifyPackageReplacedAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageA1 = new SimpleTestPackageContext("a", "1.0.0");
                var packageA2 = new SimpleTestPackageContext("a", "2.0.0");

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageA1, packageA2);

                var pathResolver = new PackagePathResolver(pathContext.SolutionRoot, useSideBySidePaths: false);

                var r1 = RunInstall(pathContext, "a", 0, "-ExcludeVersion", "-Version", "2.0.0", "-OutputDirectory", pathContext.SolutionRoot);

                // Act
                var r2 = RunInstall(pathContext, "a", 0, "-ExcludeVersion", "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot);

                var nupkgPath = pathResolver.GetInstalledPackageFilePath(new PackageIdentity("a", NuGetVersion.Parse("2.0.0")));

                // Assert
                r1.Success.Should().BeTrue();
                r2.Success.Should().BeTrue();
                File.Exists(nupkgPath).Should().BeTrue();

                using (var reader = new PackageArchiveReader(nupkgPath))
                {
                    reader.NuspecReader.GetVersion().ToNormalizedString().Should().Be("2.0.0");
                }
            }
        }

        [Fact]
        public async Task InstallCommand_InstallTwoVersionsOfAPackageVerifySxSAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageA1 = new SimpleTestPackageContext("a", "1.0.0");
                var packageA2 = new SimpleTestPackageContext("a", "2.0.0");

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageA1, packageA2);

                var pathResolver = new PackagePathResolver(pathContext.SolutionRoot, useSideBySidePaths: false);

                // Act
                var r2 = RunInstall(pathContext, "a", 0, "-Version", "2.0.0", "-OutputDirectory", pathContext.SolutionRoot);
                var r1 = RunInstall(pathContext, "a", 0, "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot);

                var nupkgPath2 = pathResolver.GetInstalledPackageFilePath(new PackageIdentity("a", NuGetVersion.Parse("2.0.0")));
                var nupkgPath1 = pathResolver.GetInstalledPackageFilePath(new PackageIdentity("a", NuGetVersion.Parse("1.0.0")));

                // Assert
                r1.Success.Should().BeTrue();
                r2.Success.Should().BeTrue();
                File.Exists(nupkgPath1).Should().BeTrue();
                File.Exists(nupkgPath2).Should().BeTrue();
            }
        }

        [Theory]
        [InlineData("net461", "c")]
        [InlineData("sl7", "b")]
        [InlineData("any", "b")]
        [InlineData("net451", "f")]
        [InlineData("native", "e")]
        [InlineData("netcoreapp2.0", "d")]
        public async Task InstallCommand_InstallWithFrameworkFlagVerifyDependenciesAsync(string tfm, string expectedId)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageA = new SimpleTestPackageContext("a", "1.0.0")
                {
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>a</id>
                            <version>1.0.0</version>
                            <title />
                            <dependencies>
                                <group>
                                    <dependency id=""b"" version=""1.0.0"" />
                                </group>
                                <group targetFramework=""net46"">
                                    <dependency id=""c"" version=""1.0.0"" />
                                </group>
                                <group targetFramework=""netstandard1.0"">
                                    <dependency id=""d"" version=""1.0.0"" />
                                </group>
                                <group targetFramework=""native"">
                                    <dependency id=""e"" version=""1.0.0"" />
                                </group>
                                <group targetFramework=""net45"">
                                    <dependency id=""f"" version=""1.0.0"" />
                                </group>
                            </dependencies>
                        </metadata>
                        </package>")
                };

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource,
                    packageA,
                    new SimpleTestPackageContext("b"),
                    new SimpleTestPackageContext("c"),
                    new SimpleTestPackageContext("d"),
                    new SimpleTestPackageContext("e"),
                    new SimpleTestPackageContext("f"));

                var pathResolver = new PackagePathResolver(pathContext.SolutionRoot, useSideBySidePaths: false);

                // Act
                var r = RunInstall(pathContext, "a", 0, "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot, "-Framework", tfm);

                var nupkgPath = pathResolver.GetInstalledPackageFilePath(new PackageIdentity(expectedId, NuGetVersion.Parse("1.0.0")));

                // Assert
                r.Success.Should().BeTrue();
                File.Exists(nupkgPath).Should().BeTrue();
                Directory.GetDirectories(pathContext.SolutionRoot).Length.Should().Be(2, "No other packages should be included");
            }
        }

        [Fact]
        public async Task InstallCommand_InstallWithUnsupportedFrameworkVerifyFailureAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageA = new SimpleTestPackageContext("a", "1.0.0");

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageA);

                var pathResolver = new PackagePathResolver(pathContext.SolutionRoot, useSideBySidePaths: false);

                // Act
                var r = RunInstall(pathContext, "a", 1, "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot, "-Framework", "blaah999");

                // Assert
                r.Success.Should().BeFalse();
                r.AllOutput.Should().Contain("'blaah999' is not a valid target framework.");
            }
        }

        [Fact]
        public void InstallCommand_FromPackagesConfigFileWithExcludeVersion()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;

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
                var r = RunInstall(pathContext, string.Empty, 0, $"-OutputDirectory outputDir -Source {repositoryPath} -ExcludeVersion");

                // Assert
                Assert.Equal(0, r.Item1);
                var packageADir = Path.Combine(workingPath, "outputDir", "packageA");
                var packageBDir = Path.Combine(workingPath, "outputDir", "packageB");
                Assert.True(Directory.Exists(packageADir));
                Assert.True(Directory.Exists(packageBDir));
            }
        }

        [Fact]
        public void InstallCommand_WithExcludeVersion()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;

                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", pathContext.PackageSource);

                // Act
                var args = new string[] {
                    "-OutputDirectory", pathContext.SolutionRoot,
                    "-Source", pathContext.PackageSource,
                    "-ExcludeVersion" };

                var r = RunInstall(pathContext, "testPackage1", 0, args);

                // Assert
                var packageDir = Path.Combine(
                    pathContext.SolutionRoot,
                    @"testPackage1");

                Assert.True(Directory.Exists(packageDir));
            }
        }

        [Fact]
        public void InstallCommand_FromPackagesConfigFile()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;

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

                var args = new string[]
                {
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    repositoryPath
                };

                // Act
                var r = RunInstall(pathContext, "", 0, args);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, "outputDir", "packageA.1.1.0", "packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, "outputDir", "packageB.2.2.0", "packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Fact]
        public void InstallCommand_FromPackagesConfigFileFailsVerifyCode()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;

                var repositoryPath = Path.Combine(workingPath, "Repository");

                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                Directory.CreateDirectory(repositoryPath);

                // Incorrect versions
                Util.CreateTestPackage("packageA", "1.0.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.0.0", repositoryPath);

                Util.CreateFile(workingPath, "packages.config",
    @"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                var args = new string[]
                {
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    repositoryPath
                };

                // Act
                var r = RunInstall(pathContext, "", 1, args);

                // Assert
                Assert.Equal(1, r.Item1);
                r.AllOutput.Should().NotContain("NU1000");
                r.Errors.Should().Contain("Unable to find version");
            }
        }

        [Fact]
        public void InstallCommand_FromPackagesConfigFile_VerifyNoopRestoreExitCode()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;

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

                var args = new string[]
                {
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    repositoryPath
                };

                // Restore 1st time
                var r = RunInstall(pathContext, "", 0, args);
                r.ExitCode.Should().Be(0);

                // Restore 2nd time
                r = RunInstall(pathContext, "", 0, args);
                r.ExitCode.Should().Be(0);
            }
        }

        [Fact]
        public void InstallCommand_ShowsAlreadyInstalledMessageWhenAllPackagesArePresent()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
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

                var args = new string[]
                {
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    repositoryPath
                };

                // Act
                var r = RunInstall(pathContext, packagesConfig, 0, args);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, "outputDir", "packageA.1.1.0", "packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, "outputDir", "packageB.2.2.0", "packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));

                //Act (Install a second time)
                var args2 = new string[]
                {
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    repositoryPath
                };

                var r1 = RunInstall(pathContext, packagesConfig, 0, args2);

                // Assert
                var message = r1.Item2;
                var alreadyInstalledMessage = string.Format("All packages listed in {0} are already installed.", packagesConfig);
                Assert.Contains(alreadyInstalledMessage, message, StringComparison.OrdinalIgnoreCase);
                r1.ExitCode.Should().Be(0);
            }
        }

        [Fact]
        public void InstallCommand_FromPackagesConfigFile_SpecifyingSolutionDir()
        {
            // Arrange
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
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

                var args = new string[]
                {
                    "-SolutionDir",
                    $"\"{workingPath}\"",
                    "-OutputDirectory",
                    "outputDir",
                    "-Source",
                    $"\"{repositoryPath}\""
                };

                // Act
                var r = RunInstall(pathContext, "", 0, args);

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

            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                var folderName = Path.GetFileName(workingPath);

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

                var args = new string[]
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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var source = pathContext.PackageSource;
                var outputDirectory = pathContext.SolutionRoot;

                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);

                // Act
                var args = new string[] {
                    "-OutputDirectory", outputDirectory,
                    "-Source", source,
                    "-PackageSaveMode", "nuspec" };

                var r = RunInstall(pathContext, "testPackage1", 0, args);

                // Assert
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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var source = pathContext.PackageSource;
                var outputDirectory = pathContext.SolutionRoot;
                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);

                // Act
                var args = new string[] {
                    "-OutputDirectory", outputDirectory,
                    "-Source", source,
                    "-PackageSaveMode", "nupkg" };

                var r = RunInstall(pathContext, "testPackage1", 0, args);

                // Assert
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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var source = pathContext.PackageSource;
                var outputDirectory = pathContext.SolutionRoot;
                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);

                // Act
                var args = new string[] {
                    "-OutputDirectory", outputDirectory,
                    "-Source", source,
                    "-PackageSaveMode", "nupkg;nuspec" };

                var r = RunInstall(pathContext, "testPackage1", 0, args);

                // Assert
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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var source = pathContext.PackageSource;
                var outputDirectory = pathContext.SolutionRoot;
                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);

                var args = new string[] {
                    "-OutputDirectory", outputDirectory,
                    "-Source", source,
                    "-PackageSaveMode", "nuspec" };
                var r = Program.Main(args);
                Assert.Equal(0, r);

                // Act
                var result = RunInstall(pathContext, "testPackage1", 0, args);

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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var source = pathContext.PackageSource;
                var outputDirectory = pathContext.SolutionRoot;
                // Arrange
                var packageFileName = Util.CreateTestPackage(
                    "testPackage1", "1.1.0", source);

                var configFile = Path.Combine(source, "nuget.config");
                Util.CreateFile(Path.GetDirectoryName(configFile), Path.GetFileName(configFile), "<configuration/>");
                var args = new string[] {
                    "config", "-Set", "PackageSaveMode=nuspec",
                    "-ConfigFile", configFile };
                var r = Program.Main(args);
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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;

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
                var r = RunInstall(pathContext, configFileName, 0, " -Source " + repositoryPath + $@" -ConfigFile my.config -RequireConsent");

                // Assert
                Assert.Equal(0, r.Item1);
                var optOutMessage = string.Format(
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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;

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
                var r = RunInstall(pathContext, configFileName, 0, " -Source " + repositoryPath + $@" -ConfigFile my.config");

                // Assert
                Assert.Equal(0, r.Item1);
                var optOutMessage = string.Format(
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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var packageDirectory = pathContext.PackageSource;

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
                var nugetexe = Util.GetNuGetExePath();

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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var packageDirectory = pathContext.PackageSource;
                var nugetexe = Util.GetNuGetExePath();

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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var packageDirectory = pathContext.PackageSource;
                var nugetexe = Util.GetNuGetExePath();

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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var packageDirectory = pathContext.PackageSource;
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);

                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                using (var server = new MockServer())
                {
                    var getPackageByVersionIsCalled = false;
                    var packageDownloadIsCalled = false;

                    server.Get.Add("/nuget/$metadata", r =>
                       Util.GetMockServerResource());
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
                    var nugetexe = Util.GetNuGetExePath();

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

        [Fact]
        public void InstallCommand_RunTwiceWithVersionSpecifiedVerifyExitCode()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var packageDirectory = pathContext.PackageSource;
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);

                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                using (var server = new MockServer())
                {
                    server.Get.Add("/nuget/$metadata", r =>
                       Util.GetMockServerResource());
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
                            response.ContentType = "application/zip";
                            using (var stream = package.GetStream())
                            {
                                var content = stream.ReadAllBytes();
                                MockServer.SetResponseContent(response, content);
                            }
                        }));

                    server.Get.Add("/nuget", r => "OK");

                    server.Start();
                    var nugetexe = Util.GetNuGetExePath();

                    // Act
                    var args = "install testPackage1 -Version 1.1.0 -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingPath,
                        args,
                        waitForExit: true);

                    var r2 = CommandRunner.Run(
                        nugetexe,
                        workingPath,
                        args,
                        waitForExit: true);

                    // Assert
                    r1.ExitCode.Should().Be(0);
                    r2.ExitCode.Should().Be(0);
                }
            }
        }

        [Fact]
        public void InstallCommand_WithVersionNotFoundVerifyExitCode()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var packageDirectory = pathContext.PackageSource;
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                // Arrange
                // Add a nuget.config to clear out sources and set the global packages folder
                Util.CreateConfigForGlobalPackagesFolder(workingPath);

                var nugetexe = Util.GetNuGetExePath();

                // Act
                var args = "install packageDoesNotExistInFolderABCX -Version 2.1.0 -Source " + pathContext.PackageSource;
                var r1 = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    args,
                    waitForExit: true);

                // Assert
                r1.ExitCode.Should().Be(1);
            }
        }

        // Tests that nuget will NOT download package from http source if the package on the server
        // has the same hash value as the cached version.
        [Fact]
        public async Task InstallCommand_WillUseCachedFileAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var packageDirectory = pathContext.PackageSource;
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");

                // Arrange

                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(pathContext.UserPackagesFolder, PackageSaveMode.Defaultv3, new PackageIdentity("testPackage1", NuGetVersion.Parse("1.1.0")));

                using (var server = new MockServer())
                {
                    var findPackagesByIdRequest = string.Empty;
                    var packageDownloadIsCalled = false;

                    server.Get.Add("/nuget/$metadata", r =>
                       Util.GetMockServerResource());
                    server.Get.Add("/nuget/FindPackagesById()", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            findPackagesByIdRequest = r.Url.ToString();
                            response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                            var feed = server.ToODataFeed(new[] { package }, "FindPackagesById");
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
                    var args = "-Source " + server.Uri + "nuget";

                    var r1 = RunInstall(pathContext, "testPackage1", 0, args);

                    // Assert
                    // verifies that package is NOT downloaded from server since nuget uses
                    // the file in machine cache.
                    Assert.False(packageDownloadIsCalled);
                }
            }
        }

        // Tests that when both the normal package and the symbol package exist in a local repository,
        // nuget install should pick the normal package.
        [Fact]
        public void InstallCommand_PreferNonSymbolPackage()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var source = pathContext.PackageSource;
                var outputDirectory = pathContext.SolutionRoot;

                // Arrange
                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage1", "1.1.0", source);
                var symbolPackageFileName = PackageCreater.CreateSymbolPackage(
                    "testPackage1", "1.1.0", source);

                var nugetexe = Util.GetNuGetExePath();

                // Act
                var args = new string[] {
                    "install", "testPackage1",
                    "-OutputDirectory", outputDirectory,
                    "-Source", source };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
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
            using (var pathContext = new SimpleTestPathContext())
            {
                var workingPath = pathContext.WorkingDirectory;
                var source = pathContext.PackageSource;
                var outputDirectory = pathContext.SolutionRoot;

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

                var nugetexe = Util.GetNuGetExePath();

                // Act
                var args = string.Format(
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

        [Theory]
        [InlineData(null, null, "1.1.0")]
        [InlineData("Lowest", "1.2", "1.2.0")]
        [InlineData("Highest", null, "2.0.0")]
        [InlineData("HighestMinor", "1.1", "1.2.0")]
        [InlineData("HighestPatch", "1.1", "1.1.1")]
        public void InstallCommand_DependencyResolution(string dependencyType, string requestedVersion, string expectedVersion)
        {
            var nugetexe = Util.GetNuGetExePath();
            using (var source = TestDirectory.Create())
            using (var outputDirectory = TestDirectory.Create())
            {
                // Arrange
                Util.CreateTestPackage("depPackage", "1.1.0", source);
                Util.CreateTestPackage("depPackage", "1.1.1", source);
                Util.CreateTestPackage("depPackage", "1.2.0", source);
                Util.CreateTestPackage("depPackage", "2.0.0", source);

                var packageFileName = PackageCreater.CreatePackage(
                    "testPackage", "1.1.0", source,
                    (builder) =>
                    {
                        if (requestedVersion == null)
                        {
                            var dependencySet = new PackageDependencySet(null,
                                new[] { new PackageDependency("depPackage") });
                            builder.DependencySets.Add(dependencySet);
                        }
                        else
                        {
                            var dependencySet = new PackageDependencySet(null,
                                new[] { new PackageDependency("depPackage",
                                    VersionUtility.ParseVersionSpec(requestedVersion)) });
                            builder.DependencySets.Add(dependencySet);
                        }
                    });

                var pathSeparator = @"\";
                // verify nuget grabs the earliest by default
                var depPackageFile = outputDirectory + $@"{pathSeparator}depPackage." + expectedVersion + $@"{pathSeparator}depPackage." + expectedVersion + ".nupkg";

                // change the path separator for mono
                if (RuntimeEnvironmentHelper.IsMono)
                {
                    depPackageFile = NuGet.Common.PathUtility.GetPathWithForwardSlashes(depPackageFile);
                }

                // Act
                string cmd;
                if (dependencyType == null)
                {
                    cmd = string.Format(
                        CultureInfo.InvariantCulture,
                        "install testPackage -OutputDirectory {0} -Source {1}", outputDirectory, source);
                }
                else
                {
                    cmd = string.Format(
                        CultureInfo.InvariantCulture,
                        "install testPackage -OutputDirectory {0} -Source {1} -DependencyVersion {2}", outputDirectory, source, dependencyType);
                }
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    cmd,
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.ExitCode);
                Assert.True(File.Exists(depPackageFile), $"File '{depPackageFile}' not found.");
            }
        }


        // Tests that when credential is saved in the config file, it will be passed
        // correctly to both the index.json endpoint and registration endpoint, even
        // though one uri does not start with the other uri.
        [Fact]
        public void InstallCommand_AuthenticatedV3WithCredentialSavedInConfig()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var pathContext = new SimpleTestPathContext())
            {
                var randomTestFolder = pathContext.WorkingDirectory;

                var credentialsPassedToRegistrationEndPoint = false;

                // Server setup
                using (var serverV3 = new MockServer())
                {
                    var registrationEndPoint = serverV3.Uri + "w";
                    var indexJson = Util.CreateIndexJson();
                    Util.AddRegistrationResource(indexJson, serverV3);

                    serverV3.Get.Add("/a/b/c/index.json", r =>
                    {
                        var h = r.Headers["Authorization"];
                        var credential = string.IsNullOrEmpty(h) ?
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
                        var credential = string.IsNullOrEmpty(h) ?
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
                    var args = new string[]
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

        /// <summary>
        /// Unit test that proves non-zero exit code functionality when the number of
        /// arguments for install command are not appropiate
        /// </summary>
        [Fact]
        public void InstallCommand_Failure_WrongArguments()
        {
            // prepare
            string[] args = {
                "install",
                "mypackage",
                "-version",
                "-outputdirectory",
            };

            var nugetexe = Util.GetNuGetExePath();

            // act & assert
            using (var testDir = TestDirectory.Create())
            {
                var result = CommandRunner.Run(
                   nugetexe,
                   testDir,
                   string.Join(" ", args),
                   true);
                Util.VerifyResultFailure(result, "'-outputdirectory' is not a valid version string.");
            }
        }

        [Theory]
        [InlineData("install mypackage -version -outputdirectory SomeDir")] // Invalid args for -version flag
        [InlineData("install a b")]
        public void InstallCommand_Failure_InvalidArguments_HelpMessage(string args)
        {
            Util.TestCommandInvalidArguments(args);
        }

        [Fact]
        public void TestInstallWhenNoFeedAvailable()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var randomTestFolder = pathContext.SolutionRoot;

                // Create an empty config file and pass it as -ConfigFile switch.
                // This imitates the scenario where there is a machine without a default nuget.config under %APPDATA%
                // In this case, nuget will not create default nuget.config for user.
                var config = string.Format(
    @"<?xml version='1.0' encoding='utf - 8'?>
<configuration/>
");
                var configFileName = Path.Combine(randomTestFolder, "nuget.config");
                File.WriteAllText(configFileName, config);

                var nugetexe = Util.GetNuGetExePath();
                var args = new string[]
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

        [Fact]
        public async Task InstallCommand_LongPathPackage()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageA = new SimpleTestPackageContext("a", "1.0.0");
                packageA.AddFile(@"content/2.5.6/core/store/x64/netcoreapp2.0/microsoft.extensions.configuration.environmentvariables/2.0.0/lib/netstandard2.0/Microsoft.Extensions.Configuration.EnvironmentVariables.dll ");

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageA);

                var pathResolver = new PackagePathResolver(pathContext.SolutionRoot, useSideBySidePaths: false);


                // Act
                var r1 = RunInstall(pathContext, "a", 0, "-Version", "1.0.0", "-OutputDirectory", pathContext.SolutionRoot);

                var nupkgPath = pathResolver.GetInstalledPackageFilePath(new PackageIdentity("a", NuGetVersion.Parse("1.0.0")));

                // Assert
                r1.Success.Should().BeTrue();
                File.Exists(nupkgPath).Should().BeTrue();
            }
        }

        public static CommandRunnerResult RunInstall(SimpleTestPathContext pathContext, string input, int expectedExitCode = 0, params string[] additionalArgs)
        {
            var nugetexe = Util.GetNuGetExePath();

            // Store the dg file for debugging
            var envVars = new Dictionary<string, string>()
            {
                { "NUGET_HTTP_CACHE_PATH", pathContext.HttpCacheFolder }
            };

            var args = new string[] {
                    "install",
                    input,
                    "-Verbosity",
                    "detailed"
                };

            args = args.Concat(additionalArgs).ToArray();

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                pathContext.WorkingDirectory,
                string.Join(" ", args),
                waitForExit: true,
                environmentVariables: envVars);

            // Assert
            Assert.True(expectedExitCode == r.Item1, r.Item3 + "\n\n" + r.Item2);

            return r;
        }
    }
}