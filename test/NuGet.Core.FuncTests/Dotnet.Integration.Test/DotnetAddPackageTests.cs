// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.XPlat.FuncTest;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection(DotnetIntegrationCollection.Name)]
    public class DotnetAddPackageTests
    {
        private readonly MsbuildIntegrationTestFixture _fixture;

        public DotnetAddPackageTests(MsbuildIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task AddPkg_V3LocalSourceFeed_WithRelativePath_NoVersionSpecified_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = "projectA";
                var targetFrameworks = "net5.0";
                SimpleTestProjectContext projectA = XPlatTestUtils.CreateProject(projectName, pathContext, targetFrameworks);
                var packageX = "packageX";
                var packageX_V1 = new PackageIdentity(packageX, new NuGetVersion("1.0.0"));
                var packageX_V2 = new PackageIdentity(packageX, new NuGetVersion("2.0.0"));
                var packageFrameworks = "net472; netcoreapp2.0";
                var packageX_V1_Context = XPlatTestUtils.CreatePackage(packageX_V1.Id, packageX_V1.Version.Version.ToString(), frameworkString: packageFrameworks);
                var packageX_V2_Context = XPlatTestUtils.CreatePackage(packageX_V2.Id, packageX_V2.Version.Version.ToString(), frameworkString: packageFrameworks);
                var customSourcePath = Path.Combine(pathContext.WorkingDirectory, "Custompackages");
                var sourceRelativePath = Path.Combine("..", "..", "Custompackages");

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    customSourcePath,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext[] { packageX_V1_Context, packageX_V2_Context });

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(projectDirectory, $"add {projectFilePath} package {packageX} -s {sourceRelativePath}", ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);

                // Make sure source is replaced in generated dgSpec file.
                PackageSpec packageSpec = projectA.AssetsFile.PackageSpec;
                string[] sources = packageSpec.RestoreMetadata.Sources.Select(s => s.Name).ToArray();
                Assert.Equal(sources.Count(), 1);
                Assert.Equal(sources[0], customSourcePath);

                var ridlessTarget = projectA.AssetsFile.Targets.Where(e => string.IsNullOrEmpty(e.RuntimeIdentifier)).Single();
                ridlessTarget.Libraries.Should().Contain(e => e.Type == "package" && e.Name == packageX);
                // Should resolve to specified package.
                ridlessTarget.Libraries.Should().Contain(e => e.Version.Equals(packageX_V2.Version));
            }
        }

        [Fact]
        public async Task AddPkg_V3LocalSourceFeed_WithRelativePath_NoVersionSpecified_Fail()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = "projectA";
                var targetFrameworks = "net5.0";
                XPlatTestUtils.CreateProject(projectName, pathContext, targetFrameworks);
                var packageX = "packageX";
                var packageY = "packageY";
                var packageY_V1 = new PackageIdentity(packageY, new NuGetVersion("1.0.0"));
                var packageFrameworks = "net472; netcoreapp2.0";
                var packageY_V1_Context = XPlatTestUtils.CreatePackage(packageY_V1.Id, packageY_V1.Version.Version.ToString(), frameworkString: packageFrameworks);
                var customSourcePath = Path.Combine(pathContext.WorkingDirectory, "Custompackages");
                var sourceRelativePath = Path.Combine("..", "..", "Custompackages");

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    customSourcePath,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext[] { packageY_V1_Context });

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(projectDirectory, $"add {projectFilePath} package {packageX} -s {sourceRelativePath}", ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse(because: result.AllOutput);
            }
        }

        [Fact]
        public async Task AddPkg_V3LocalSourceFeed_WithRelativePath_VersionSpecified_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = "projectA";
                var targetFrameworks = "net5.0";
                SimpleTestProjectContext projectA = XPlatTestUtils.CreateProject(projectName, pathContext, targetFrameworks);
                var packageX = "packageX";
                var packageX_V1 = new PackageIdentity(packageX, new NuGetVersion("1.0.0"));
                var packageX_V2 = new PackageIdentity(packageX, new NuGetVersion("2.0.0"));
                var packageFrameworks = "net472; netcoreapp2.0";
                var packageX_V1_Context = XPlatTestUtils.CreatePackage(packageX_V1.Id, packageX_V1.Version.Version.ToString(), frameworkString: packageFrameworks);
                var packageX_V2_Context = XPlatTestUtils.CreatePackage(packageX_V2.Id, packageX_V2.Version.Version.ToString(), frameworkString: packageFrameworks);
                var customSourcePath = Path.Combine(pathContext.WorkingDirectory, "Custompackages");
                var sourceRelativePath = Path.Combine("..", "..", "Custompackages");

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    customSourcePath,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext[] { packageX_V1_Context, packageX_V2_Context });

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(projectDirectory, $"add {projectFilePath} package {packageX} -s {sourceRelativePath} -v {packageX_V1.Version}", ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);

                // Make sure source is replaced in generated dgSpec file.
                PackageSpec packageSpec = projectA.AssetsFile.PackageSpec;
                string[] sources = packageSpec.RestoreMetadata.Sources.Select(s => s.Name).ToArray();
                Assert.Equal(sources.Count(), 1);
                Assert.Equal(sources[0], customSourcePath);

                var ridlessTarget = projectA.AssetsFile.Targets.Where(e => string.IsNullOrEmpty(e.RuntimeIdentifier)).Single();
                ridlessTarget.Libraries.Should().Contain(e => e.Type == "package" && e.Name == packageX);
                // Should resolve to specified package.
                ridlessTarget.Libraries.Should().Contain(e => e.Version.Equals(packageX_V1.Version));
            }
        }

        [Fact]
        public async Task AddPkg_V3LocalSourceFeed_WithRelativePath_VersionSpecified_Fail()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = "projectA";
                var targetFrameworks = "net5.0";
                SimpleTestProjectContext projectA = XPlatTestUtils.CreateProject(projectName, pathContext, targetFrameworks);
                var packageX = "packageX";
                var packageX_V1 = new PackageIdentity(packageX, new NuGetVersion("1.0.0"));
                var packageX_V2 = new PackageIdentity(packageX, new NuGetVersion("2.0.0"));
                var packageFrameworks = "net472; netcoreapp2.0";
                var packageX_V1_Context = XPlatTestUtils.CreatePackage(packageX_V1.Id, packageX_V1.Version.Version.ToString(), frameworkString: packageFrameworks);
                var customSourcePath = Path.Combine(pathContext.WorkingDirectory, "Custompackages");
                var sourceRelativePath = Path.Combine("..", "..", "Custompackages");

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    customSourcePath,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext[] { packageX_V1_Context });

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(projectDirectory, $"add {projectFilePath} package {packageX} -s {sourceRelativePath} -v {packageX_V2.Version}", ignoreExitCode: true);

                // Assert
                result.Success.Should().BeFalse(because: result.AllOutput);
            }
        }

        [Fact]
        public async Task AddPkg_V3LocalSourceFeed_WithDefaultSolutiuonSource_NoVersionSpecified_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = "projectA";
                var targetFrameworks = "net5.0";
                SimpleTestProjectContext projectA = XPlatTestUtils.CreateProject(projectName, pathContext, targetFrameworks);
                var packageY = "packageY";
                var packageY_V1 = new PackageIdentity(packageY, new NuGetVersion("1.0.0"));
                var packageY_V2 = new PackageIdentity(packageY, new NuGetVersion("2.0.0"));
                var packageFrameworks = "net472; netcoreapp2.0";
                var packageY_V1_Context = XPlatTestUtils.CreatePackage(packageY_V1.Id, packageY_V1.Version.Version.ToString(), frameworkString: packageFrameworks);
                var packageY_V2_Context = XPlatTestUtils.CreatePackage(packageY_V2.Id, packageY_V2.Version.Version.ToString(), frameworkString: packageFrameworks);

                // Generate V3 Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource, // using default solution source folder, not passing source as parameter.
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext[] { packageY_V1_Context, packageY_V2_Context });

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(projectDirectory, $"add {projectFilePath} package {packageY}", ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);

                // Make sure source is replaced in generated dgSpec file.
                PackageSpec packageSpec = projectA.AssetsFile.PackageSpec;
                string[] sources = packageSpec.RestoreMetadata.Sources.Select(s => s.Name).ToArray();
                Assert.Equal(sources.Count(), 1);
                Assert.Equal(sources[0], pathContext.PackageSource);

                var ridlessTarget = projectA.AssetsFile.Targets.Where(e => string.IsNullOrEmpty(e.RuntimeIdentifier)).Single();
                // Should resolve to specified package.
                ridlessTarget.Libraries.Should().Contain(e => e.Type == "package" && e.Name == packageY);
                // Should resolve to highest available version.
                ridlessTarget.Libraries.Should().Contain(e => e.Version.Equals(packageY_V2.Version));
            }
        }

        [Fact]
        public async Task AddPkg_WhenPackageSourceMappingConfiguredAndNoMatchingSourceFound_Fails()
        {
            using var pathContext = new SimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projectName = "projectA";
            var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, "net5.0");

            const string version = "1.0.0";
            const string packageX = "X", packageZ = "Z";

            var packageFrameworks = "net472; net5.0";
            var packageX100 = XPlatTestUtils.CreatePackage(packageX, version, frameworkString: packageFrameworks);
            var packageZ100 = XPlatTestUtils.CreatePackage(packageZ, version, frameworkString: packageFrameworks);

            packageX100.Dependencies.Add(packageZ100);

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var packageSource2 = new DirectoryInfo(Path.Combine(pathContext.WorkingDirectory, "source2"));
            packageSource2.Create();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource2.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source2"" value=""{packageSource2.FullName}"" />
    </packageSources>
        <packageSourceMapping>
            <packageSource key=""source2"">
                <package pattern=""{packageX}*"" />
            </packageSource>
    </packageSourceMapping>
</configuration>
";
            var projectADirectory = Path.Combine(pathContext.SolutionRoot, projectA.ProjectName);
            File.WriteAllText(Path.Combine(projectADirectory, "NuGet.Config"), configFile);

            //Act
            var result = _fixture.RunDotnet(projectADirectory, $"add {projectA.ProjectPath} package {packageX} -v {version}", ignoreExitCode: true);

            // Assert
            result.Success.Should().BeFalse(because: result.AllOutput);
            Assert.Contains($"Installed {packageX} {version} from {packageSource2.FullName}", result.AllOutput);
            Assert.Contains($"NU1100: Unable to resolve '{packageZ} (>= {version})'", result.AllOutput);
        }

        [Fact]
        public async Task AddPkg_WhenPackageSourceMappingConfiguredInstallsPackagesFromExpectedSources_Success()
        {
            using var pathContext = new SimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projectName = "projectA";
            var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, "net5.0");

            const string version = "1.0.0";
            const string packageX = "X", packageZ = "Z";

            var packageFrameworks = "net472; net5.0";
            var packageX100 = XPlatTestUtils.CreatePackage(packageX, version, frameworkString: packageFrameworks);
            var packageZ100 = XPlatTestUtils.CreatePackage(packageZ, version, frameworkString: packageFrameworks);

            packageX100.Dependencies.Add(packageZ100);

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var packageSource2 = new DirectoryInfo(Path.Combine(pathContext.WorkingDirectory, "source2"));
            packageSource2.Create();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource2.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source2"" value=""{packageSource2.FullName}"" />
    </packageSources>
        <packageSourceMapping>
            <packageSource key=""source"">
                <package pattern=""{packageZ}*"" />
            </packageSource>
            <packageSource key=""source2"">
                <package pattern=""{packageX}*"" />
            </packageSource>
    </packageSourceMapping>
</configuration>
";
            var projectADirectory = Path.Combine(pathContext.SolutionRoot, projectA.ProjectName);
            File.WriteAllText(Path.Combine(projectADirectory, "NuGet.Config"), configFile);

            //Act
            var result = _fixture.RunDotnet(projectADirectory, $"add {projectA.ProjectPath} package {packageX} -v {version}", ignoreExitCode: true);

            // Assert
            result.Success.Should().BeTrue(because: result.AllOutput);
            Assert.Contains($"Installed {packageX} {version} from {packageSource2.FullName}", result.AllOutput);
            Assert.Contains($"Installed {packageZ} {version} from {pathContext.PackageSource}", result.AllOutput);
        }

        [Fact]
        public async Task AddPkg_WhenPackageSourceMappingConfiguredInstallsPackagesFromSourcesUriOption_Success()
        {
            using var pathContext = new SimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projectName = "projectA";
            var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, "net5.0");

            const string version = "1.0.0";
            const string packageX = "X", packageZ = "Z";

            var packageFrameworks = "net472; net5.0";
            var packageX100 = XPlatTestUtils.CreatePackage(packageX, version, frameworkString: packageFrameworks);
            var packageZ100 = XPlatTestUtils.CreatePackage(packageZ, version, frameworkString: packageFrameworks);

            packageX100.Dependencies.Add(packageZ100);

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var packageSource2 = new DirectoryInfo(Path.Combine(pathContext.WorkingDirectory, "source2"));
            packageSource2.Create();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource2.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source2"" value=""{packageSource2.FullName}"" />
    </packageSources>
        <packageSourceMapping>
            <packageSource key=""source"">
                <package pattern=""{packageZ}*"" />
                <package pattern=""{packageX}*"" />
            </packageSource>
            <packageSource key=""source2"">
                <package pattern=""{packageZ}*"" />
                <package pattern=""{packageX}*"" />
            </packageSource>
    </packageSourceMapping>
</configuration>
";
            var projectADirectory = Path.Combine(pathContext.SolutionRoot, projectA.ProjectName);
            File.WriteAllText(Path.Combine(projectADirectory, "NuGet.Config"), configFile);

            //Act
            var result = _fixture.RunDotnet(projectADirectory, $"add {projectA.ProjectPath} package {packageX} -v {version} -s {pathContext.PackageSource}", ignoreExitCode: true);

            // Assert
            result.Success.Should().BeTrue(because: result.AllOutput);
            Assert.Contains($"Installed {packageX} {version} from {pathContext.PackageSource}", result.AllOutput);
            Assert.Contains($"Installed {packageZ} {version} from {pathContext.PackageSource}", result.AllOutput);
        }

        [Fact]
        public async Task AddPkg_WhenPackageSourceMappingConfiguredCanotInstallsPackagesFromSourcesUriOption_Fails()
        {
            using var pathContext = new SimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projectName = "projectA";
            var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, "net5.0");

            const string version = "1.0.0";
            const string packageX = "X", packageZ = "Z";

            var packageFrameworks = "net472; net5.0";
            var packageX100 = XPlatTestUtils.CreatePackage(packageX, version, frameworkString: packageFrameworks);
            var packageZ100 = XPlatTestUtils.CreatePackage(packageZ, version, frameworkString: packageFrameworks);

            packageX100.Dependencies.Add(packageZ100);

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var packageSource2 = new DirectoryInfo(Path.Combine(pathContext.WorkingDirectory, "source2"));
            packageSource2.Create();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource2.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source2"" value=""{packageSource2.FullName}"" />
    </packageSources>
        <packageSourceMapping>
            <packageSource key=""source"">
                <package pattern=""{packageZ}*"" />
                <package pattern=""{packageX}*"" />
            </packageSource>
            <packageSource key=""source2"">
                <package pattern=""{packageX}*"" />
            </packageSource>
    </packageSourceMapping>
</configuration>
";
            var projectADirectory = Path.Combine(pathContext.SolutionRoot, projectA.ProjectName);
            File.WriteAllText(Path.Combine(projectADirectory, "NuGet.Config"), configFile);

            //Act
            var result = _fixture.RunDotnet(projectADirectory, $"add {projectA.ProjectPath} package {packageX} -v {version} -s {packageSource2}", ignoreExitCode: true);

            // Assert
            result.Success.Should().BeFalse(because: result.AllOutput);
            Assert.Contains($"Installed {packageX} {version} from {packageSource2}", result.AllOutput);
            Assert.Contains($"NU1100: Unable to resolve '{packageZ} (>= {version})' for 'net5.0'", result.AllOutput);
        }

        [Fact]
        public async Task AddPkg_WhenPackageSourceMappingConfiguredInstallsPackagesFromRestoreSources_Success()
        {
            using var pathContext = new SimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projectName = "projectA";
            var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, "net5.0");

            const string version = "1.0.0";
            const string packageX = "X", packageZ = "Z";

            var packageFrameworks = "net472;net5.0";
            var packageX100 = XPlatTestUtils.CreatePackage(packageX, version, frameworkString: packageFrameworks);
            var packageZ100 = XPlatTestUtils.CreatePackage(packageZ, version, frameworkString: packageFrameworks);

            packageX100.Dependencies.Add(packageZ100);

            var packageSource2 = new DirectoryInfo(Path.Combine(pathContext.WorkingDirectory, "source2"));
            packageSource2.Create();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource2.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source2"" value=""{packageSource2.FullName}"" />
    </packageSources>
        <packageSourceMapping>
            <packageSource key=""source"">
                <package pattern=""*"" />
            </packageSource>
            <packageSource key=""source2"">
                <package pattern=""{packageZ}*"" />
                <package pattern=""{packageX}*"" />
            </packageSource>
    </packageSourceMapping>
</configuration>
";

            // Add RestoreSources
            projectA.Properties.Add("RestoreSources", $"{packageSource2.FullName}");

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var projectADirectory = Path.Combine(pathContext.SolutionRoot, projectA.ProjectName);
            File.WriteAllText(Path.Combine(projectADirectory, "NuGet.Config"), configFile);

            //Act
            var result = _fixture.RunDotnet(projectADirectory, $"add {projectA.ProjectPath} package {packageX} -v {version}", ignoreExitCode: true);

            // Assert
            result.Success.Should().BeTrue(because: result.AllOutput);
            Assert.Contains($"Installed {packageX} {version} from {packageSource2.FullName}", result.AllOutput);
            Assert.Contains($"Installed {packageZ} {version} from {packageSource2.FullName}", result.AllOutput);
        }

        [Fact]
        public async Task AddPkg_WhenPackageSourceMappingConfiguredCanotInstallsPackagesFromRestoreSources_Fails()
        {
            using var pathContext = new SimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projectName = "projectA";
            var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, "net5.0");

            const string version = "1.0.0";
            const string packageX = "X", packageZ = "Z";

            var packageFrameworks = "net472; net5.0";
            var packageX100 = XPlatTestUtils.CreatePackage(packageX, version, frameworkString: packageFrameworks);
            var packageZ100 = XPlatTestUtils.CreatePackage(packageZ, version, frameworkString: packageFrameworks);

            packageX100.Dependencies.Add(packageZ100);

            var packageSource2 = new DirectoryInfo(Path.Combine(pathContext.WorkingDirectory, "source2"));
            packageSource2.Create();

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource2.FullName,
                    PackageSaveMode.Defaultv3,
                    packageX100,
                    packageZ100);

            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source2"" value=""{packageSource2.FullName}"" />
    </packageSources>
        <packageSourceMapping>
            <packageSource key=""source2"">
                <package pattern=""{packageX}*"" />
            </packageSource>
    </packageSourceMapping>
</configuration>
";
            // Add RestoreSources
            projectA.Properties.Add("RestoreSources", $"{packageSource2.FullName};{pathContext.PackageSource}");

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var projectADirectory = Path.Combine(pathContext.SolutionRoot, projectA.ProjectName);
            File.WriteAllText(Path.Combine(projectADirectory, "NuGet.Config"), configFile);

            //Act
            var result = _fixture.RunDotnet(projectADirectory, $"add {projectA.ProjectPath} package {packageX} -v {version} -s {packageSource2}", ignoreExitCode: true);

            // Assert
            result.Success.Should().BeFalse(because: result.AllOutput);
            Assert.Contains($"Installed {packageX} {version} from {packageSource2}", result.AllOutput);
            Assert.Contains($"NU1100: Unable to resolve '{packageZ} (>= {version})' for 'net5.0'", result.AllOutput);
        }

        [Fact]
        public async Task AddPkg_WhenProjectOnboardedToCPMAndNoPackagesExistInProps()
        {
            using var pathContext = new SimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projectName = "projectA";
            var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, "net5.0");

            const string version = "1.0.0";
            const string packageX = "X";

            var packageFrameworks = "net5.0";
            var packageX100 = XPlatTestUtils.CreatePackage(packageX, version, frameworkString: packageFrameworks);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100);

            var propsFile = @$"<Project>
                                <PropertyGroup>
                                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                                </PropertyGroup>
                            </Project>
                            ";

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var projectADirectory = Path.Combine(pathContext.SolutionRoot, projectA.ProjectName);
            File.WriteAllText(Path.Combine(projectADirectory, "Directory.Packages.Props"), propsFile);

            //Act
            var result = _fixture.RunDotnet(projectADirectory, $"add {projectA.ProjectPath} package {packageX}", ignoreExitCode: true);

            // Assert
            Assert.Contains(@$"<ItemGroup>
    <PackageVersion Include=""X"" Version=""1.0.0"" />
  </ItemGroup", File.ReadAllText(Path.Combine(projectADirectory, "Directory.Packages.Props")));
            Assert.DoesNotContain(@$"<ItemGroup> <PackageVersion Include=""X"" Version=""1.0.0"" /> </ItemGroup",
                File.ReadAllText(Path.Combine(projectADirectory, "projectA.csproj")));
        }

        [Fact]
        public async Task AddPkg_WhenProjectOnboardedToCPMAndPackagesExistInProps()
        {
            using var pathContext = new SimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projectName = "projectA";
            var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, "net5.0");

            const string version = "1.0.0";
            const string packageX = "X";

            var packageFrameworks = "net5.0";
            var packageX100 = XPlatTestUtils.CreatePackage(packageX, version, frameworkString: packageFrameworks);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100);

            var propsFile = @$"<Project>
                                <PropertyGroup>
                                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                                </PropertyGroup>
                                <ItemGroup>
                                <PackageVersion Include=""Y"" Version=""1.0.0"" />
                                </ItemGroup>
                            </Project>
                            ";

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var projectADirectory = Path.Combine(pathContext.SolutionRoot, projectA.ProjectName);
            File.WriteAllText(Path.Combine(projectADirectory, "Directory.Packages.Props"), propsFile);

            //Act
            var result = _fixture.RunDotnet(projectADirectory, $"add {projectA.ProjectPath} package {packageX}", ignoreExitCode: true);

            // Assert
            Assert.Contains(@$"<PackageVersion Include=""X"" Version=""1.0.0"" />", File.ReadAllText(Path.Combine(projectADirectory, "Directory.Packages.Props")));
            Assert.DoesNotContain(@$"Include=""X"" Version=""1.0.0""", File.ReadAllText(Path.Combine(projectADirectory, "projectA.csproj")));
        }

        [Fact]
        public async Task AddPkg_WhenProjectOnboardedToCPMAndPackageNeedsUpdate()
        {
            using var pathContext = new SimpleTestPathContext();

            // Set up solution, and project
            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var projectName = "projectA";
            var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, "net5.0");

            const string version = "2.0.0";
            const string packageX = "X";

            var packageFrameworks = "net5.0";
            var packageX100 = XPlatTestUtils.CreatePackage(packageX, version, frameworkString: packageFrameworks);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX100);

            var propsFile = @$"<Project>
                                <PropertyGroup>
                                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                                </PropertyGroup>
                                <ItemGroup>
                                <PackageVersion Include=""X"" Version=""1.0.0"" />
                                </ItemGroup>
                            </Project>
                            ";

            solution.Projects.Add(projectA);
            solution.Create(pathContext.SolutionRoot);

            var projectADirectory = Path.Combine(pathContext.SolutionRoot, projectA.ProjectName);
            File.WriteAllText(Path.Combine(projectADirectory, "Directory.Packages.Props"), propsFile);

            //Act
            var result = _fixture.RunDotnet(projectADirectory, $"add {projectA.ProjectPath} package {packageX} -v {version}", ignoreExitCode: true);

            // Assert
            Assert.Contains(@$"<PackageVersion Include=""X"" Version=""2.0.0"" />", File.ReadAllText(Path.Combine(projectADirectory, "Directory.Packages.Props")));
            Assert.DoesNotContain(@$"Include=""X"" Version=""2.0.0""", File.ReadAllText(Path.Combine(projectADirectory, "projectA.csproj")));
        }
    }
}



