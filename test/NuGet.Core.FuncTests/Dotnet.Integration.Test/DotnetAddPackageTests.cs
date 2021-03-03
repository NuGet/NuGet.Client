// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
    [Collection("Dotnet Integration Tests")]
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
    }
}
