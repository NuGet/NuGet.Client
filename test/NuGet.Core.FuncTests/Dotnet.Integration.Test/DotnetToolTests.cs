// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.IO;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetToolTests
    {
        private MsbuildIntegrationTestFixture _msbuildFixture;

        public DotnetToolTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp1.0")]
        public void DotnetToolTests_NoPackageReferenceToolRestore_ThrowsError(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = workingDirectory;
                var rid = "win-x86";
                var packages = new List<PackageIdentity>();

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: workingDirectory, packages: packages);
                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("NU1211", result.Item2);
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp1.0")]
        public void DotnetToolTests_RegularDependencyPackageWithDependenciesToolRestore_ThrowsError(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = "https://api.nuget.org/v3/index.json";
                var rid = "win-x86";
                var packages = new List<PackageIdentity>() { new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("10.0.3")) };

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("NU1203", result.Item2);
                Assert.DoesNotContain("NU1211", result.Item2); // It's the correct dependency count!
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("net45")]
        [InlineData("netcoreapp1.0")]
        [InlineData("netcoreapp2.0")]
        public void DotnetToolTests_BasicDotnetToolRestore_Succeeds(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = Path.Combine(testDirectory, "packageSource");
                var rid = "win-x64"; // Does x64 make a difference here?
                var packageName = string.Join("ToolPackage-", tfm, rid);
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var packages = new List<PackageIdentity>() { new PackageIdentity(packageName, packageVersion) };

                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/{rid}/a.dll");
                package.PackageType = PackageType.DotnetTool;
                package.PackageTypes.Add(PackageType.DotnetTool);
                SimpleTestPackageUtility.CreatePackages(source, package);

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: rid,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 0, result.AllOutput);

                // Verify the assets file
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net461")]
        [InlineData("netcoreapp1.0")]
        [InlineData("netcoreapp2.0")]
        public void DotnetToolTests_MismatchedRID_Fails(string tfm)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ToolRestoreProject";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var source = Path.Combine(testDirectory, "packageSource");
                var projectRID = "win-x64";
                var packageRID = "win-x86";

                var packageName = string.Join("ToolPackage-", tfm, packageRID);
                var packageVersion = NuGetVersion.Parse("1.0.0");
                var packages = new List<PackageIdentity>() { new PackageIdentity(packageName, packageVersion)};

                var package = new SimpleTestPackageContext(packageName, packageVersion.OriginalVersion);
                package.Files.Clear();
                package.AddFile($"tools/{tfm}/{packageRID}/a.dll");
                package.PackageTypes.Add(PackageType.DotnetTool);
                SimpleTestPackageUtility.CreatePackages(source, package);

                _msbuildFixture.CreateDotnetToolProject(solutionRoot: testDirectory.Path,
                    projectName: projectName, targetFramework: tfm, rid: projectRID,
                    source: source, packages: packages);

                // Act
                var result = _msbuildFixture.RestoreToolProject(workingDirectory, projectName, string.Empty);

                // Assert
                Assert.True(result.Item1 == 1, result.AllOutput);
                Assert.Contains("NU1202", result.Item2);
            }
        }
    }
}
