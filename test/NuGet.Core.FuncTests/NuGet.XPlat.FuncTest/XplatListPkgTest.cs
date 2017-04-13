// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Commands;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XplatListPkgTest
    {

        private static readonly string projectName = "test_project_listpkg";

        private static MSBuildAPIUtility MsBuild => new MSBuildAPIUtility(new TestCommandOutputLogger());
        
        // Argument parsing related tests

        [Theory]
        [InlineData("--package", "package_foo", "--project", "project_foo.csproj", "", "")]
        [InlineData("--package", "package_foo", "-p", "project_foo.csproj", "--framework", "net46;netcoreapp1.0")]
        [InlineData("--package", "package_foo", "-p", "project_foo.csproj", "-f", "net46 ; netcoreapp1.0 ; ")]
        [InlineData("--package", "package_foo", "-p", "project_foo.csproj", "-f", "net46")]
        [InlineData("", "", "--project", "project_foo.csproj", "", "")]
        public void ListPkg_ArgParsing(string packageOption, string packageString,
        string projectOption, string project, string frameworkOption, string frameworkString)
        {
            // Arrange
            var projectPath = Path.Combine(Path.GetTempPath(), project);
            File.Create(projectPath).Dispose();

            var argList = new List<string>() {
                "list",
                projectOption,
                projectPath};

            if (!string.IsNullOrEmpty(frameworkOption))
            {
                argList.Add(frameworkOption);
                argList.Add(frameworkString);
            }

            if (!string.IsNullOrEmpty(packageOption))
            {
                argList.Add(packageOption);
                argList.Add(packageString);
            }

            var logger = new TestCommandOutputLogger();
            var testApp = new CommandLineApplication();
            var mockCommandRunner = new Mock<IPackageReferenceCommandRunner>();
            mockCommandRunner
                .Setup(m => m.ExecuteCommand(It.IsAny<PackageReferenceArgs>(), It.IsAny<MSBuildAPIUtility>()))
                .ReturnsAsync(0);

            testApp.Name = "dotnet nuget_test";
            ListPackageReferenceCommand.Register(testApp,
                () => logger,
                () => mockCommandRunner.Object);

            // Act
            var result = testApp.Execute(argList.ToArray());

            XPlatTestUtils.DisposeTemporaryFile(projectPath);

            // Assert
            mockCommandRunner.Verify(m => m.ExecuteCommand(It.Is<PackageReferenceArgs>(p =>
            p.ProjectPath == projectPath &&
            (string.IsNullOrEmpty(packageOption) || (!string.IsNullOrEmpty(packageOption) && p.PackageDependency.Id.Equals(packageString))) &&
            (string.IsNullOrEmpty(frameworkOption) || (!string.IsNullOrEmpty(frameworkOption) && p.Frameworks.SequenceEqual(MSBuildStringUtility.Split(frameworkString))))),
            It.IsAny<MSBuildAPIUtility>()));

            Assert.Equal(0, result);
        }

        // List Related Tests

        [Fact]
        public async void ListPkg_UnconditionalReferences()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Generate Package
                var packageX = XPlatTestUtils.CreatePackage();
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, packageX, "netcoreapp1.0");

                // Verify that the package reference exists before removing.
                var projectXmlRoot = XPlatTestUtils.LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = XPlatTestUtils.GetItemGroupForAllFrameworks(projectXmlRoot);

                Assert.NotNull(itemGroup);
                Assert.True(XPlatTestUtils.ValidateReference(itemGroup, packageX.Id, "1.0.0"));

                // Act
                var packageReferences = MsBuild.ListPackageReference(projectA.ProjectPath, packageDependency: null, userInputFrameworks: new string[0]);

                // Assert
                Assert.True(packageReferences.ContainsKey("All Frameworks"));
                Assert.True(packageReferences["All Frameworks"].Where(p => p.Item1.Equals(packageX.Id)).Count() == 1);
            }
        }

        [Fact]
        public async void ListPkg_OneConditionalReferences()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Generate Package
                var packageX = XPlatTestUtils.CreatePackage(frameworkString:"netcoreapp1.0");
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, packageX, 
                    projectFrameworks: "netcoreapp1.0;netstandard1.3;", packageFramework: "netcoreapp1.0");

                // Verify that the package reference exists before removing.
                var projectXmlRoot = XPlatTestUtils.LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = XPlatTestUtils.GetItemGroupForFramework(projectXmlRoot, "netcoreapp1.0");

                Assert.NotNull(itemGroup);
                Assert.True(XPlatTestUtils.ValidateReference(itemGroup, packageX.Id, "1.0.0"));

                // Act
                var packageReferences = MsBuild.ListPackageReference(projectA.ProjectPath, packageDependency: null, userInputFrameworks: new string[0]);

                // Assert
                Assert.True(packageReferences.ContainsKey("netcoreapp1.0"));
                Assert.True(packageReferences["netcoreapp1.0"].Where(p => p.Item1.Equals(packageX.Id)).Count() == 1);
            }
        }

        [Fact]
        public async void ListPkg_MultipleConditionalReferences()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Generate Package
                var packageX = XPlatTestUtils.CreatePackage(packageId: "packageX", frameworkString: "netcoreapp1.0");
                var packageY = XPlatTestUtils.CreatePackage(packageId: "packageY", frameworkString: "netstandard1.3");
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, packageX,
                    projectFrameworks: "netcoreapp1.0;netstandard1.3;", packageFramework: "netcoreapp1.0");

                projectA.AddPackageToFramework("netstandard1.3", packageY);

                projectA.Save();

                // Verify that the package reference exists before removing.
                var projectXmlRoot = XPlatTestUtils.LoadCSProj(projectA.ProjectPath).Root;
                var itemGroupNetCore = XPlatTestUtils.GetItemGroupForFramework(projectXmlRoot, "netcoreapp1.0");
                var itemGroupNetStandard = XPlatTestUtils.GetItemGroupForFramework(projectXmlRoot, "netstandard1.3");

                Assert.NotNull(itemGroupNetCore);
                Assert.True(XPlatTestUtils.ValidateReference(itemGroupNetCore, packageX.Id, "1.0.0"));
                Assert.NotNull(itemGroupNetStandard);
                Assert.True(XPlatTestUtils.ValidateReference(itemGroupNetStandard, packageY.Id, "1.0.0"));

                // Act
                var packageReferences = MsBuild.ListPackageReference(projectA.ProjectPath, packageDependency: null, userInputFrameworks: new string[0]);

                // Assert
                Assert.True(packageReferences.ContainsKey("netcoreapp1.0"));
                Assert.True(packageReferences["netcoreapp1.0"].Where(p => p.Item1.Equals(packageX.Id)).Count() == 1);
                Assert.True(packageReferences.ContainsKey("netstandard1.3"));
                Assert.True(packageReferences["netstandard1.3"].Where(p => p.Item1.Equals(packageY.Id)).Count() == 1);
            }
        }


        [Fact]
        public async void ListPkg_MultipleConditionalWithPackageFilterReferences()
        {
            // Arrange
            //XPlatTestUtils.WaitForDebugger();
            using (var pathContext = new SimpleTestPathContext())
            {
                // Generate Package
                var packageX = XPlatTestUtils.CreatePackage(packageId: "packageX", frameworkString: "netcoreapp1.0");
                var packageY = XPlatTestUtils.CreatePackage(packageId: "packageY", frameworkString: "netstandard1.3");
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, packageX,
                    projectFrameworks: "netcoreapp1.0;netstandard1.3;", packageFramework: "netcoreapp1.0");

                projectA.AddPackageToFramework("netstandard1.3", packageY);

                projectA.Save();

                // Verify that the package reference exists before removing.
                var projectXmlRoot = XPlatTestUtils.LoadCSProj(projectA.ProjectPath).Root;
                var itemGroupNetCore = XPlatTestUtils.GetItemGroupForFramework(projectXmlRoot, "netcoreapp1.0");
                var itemGroupNetStandard = XPlatTestUtils.GetItemGroupForFramework(projectXmlRoot, "netstandard1.3");

                Assert.NotNull(itemGroupNetCore);
                Assert.True(XPlatTestUtils.ValidateReference(itemGroupNetCore, packageX.Id, "1.0.0"));
                Assert.NotNull(itemGroupNetStandard);
                Assert.True(XPlatTestUtils.ValidateReference(itemGroupNetStandard, packageY.Id, "1.0.0"));

                // Act
                var PackageXReferences = MsBuild.ListPackageReference(projectA.ProjectPath, 
                    packageDependency: new PackageDependency(packageX.Id, VersionRange.Parse("*")), 
                    userInputFrameworks: new string[0]);

                var PackageYReferences = MsBuild.ListPackageReference(projectA.ProjectPath, 
                    packageDependency: new PackageDependency(packageY.Id, VersionRange.Parse("*")), 
                    userInputFrameworks: new string[0]);

                // Assert
                Assert.True(PackageXReferences.ContainsKey("netcoreapp1.0"));
                Assert.True(PackageXReferences["netcoreapp1.0"].Where(p => p.Item1.Equals(packageX.Id)).Count() == 1);
                Assert.False(PackageXReferences["netstandard1.3"].Any());

                Assert.True(PackageYReferences.ContainsKey("netstandard1.3"));
                Assert.True(PackageYReferences["netstandard1.3"].Where(p => p.Item1.Equals(packageY.Id)).Count() == 1);
                Assert.False(PackageYReferences["netcoreapp1.0"].Any());
            }
        }

        [Fact]
        public async void ListPkg_MultipleConditionalWithFrameworkFilterReferences()
        {
            // Arrange
            //XPlatTestUtils.WaitForDebugger();
            using (var pathContext = new SimpleTestPathContext())
            {
                // Generate Package
                var packageX = XPlatTestUtils.CreatePackage(packageId: "packageX", frameworkString: "netcoreapp1.0");
                var packageY = XPlatTestUtils.CreatePackage(packageId: "packageY", frameworkString: "netstandard1.3");
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var projectA = XPlatTestUtils.CreateProject(projectName, pathContext, packageX,
                    projectFrameworks: "netcoreapp1.0;netstandard1.3;", packageFramework: "netcoreapp1.0");

                projectA.AddPackageToFramework("netstandard1.3", packageY);

                projectA.Save();

                // Verify that the package reference exists before removing.
                var projectXmlRoot = XPlatTestUtils.LoadCSProj(projectA.ProjectPath).Root;
                var itemGroupNetCore = XPlatTestUtils.GetItemGroupForFramework(projectXmlRoot, "netcoreapp1.0");
                var itemGroupNetStandard = XPlatTestUtils.GetItemGroupForFramework(projectXmlRoot, "netstandard1.3");

                Assert.NotNull(itemGroupNetCore);
                Assert.True(XPlatTestUtils.ValidateReference(itemGroupNetCore, packageX.Id, "1.0.0"));
                Assert.NotNull(itemGroupNetStandard);
                Assert.True(XPlatTestUtils.ValidateReference(itemGroupNetStandard, packageY.Id, "1.0.0"));

                // Act
                var PackageXReferences = MsBuild.ListPackageReference(projectA.ProjectPath,
                    packageDependency: new PackageDependency(packageX.Id, VersionRange.Parse("*")),
                    userInputFrameworks: new string[0]);

                var PackageYReferences = MsBuild.ListPackageReference(projectA.ProjectPath,
                    packageDependency: new PackageDependency(packageY.Id, VersionRange.Parse("*")),
                    userInputFrameworks: new string[0]);

                // Assert
                Assert.True(PackageXReferences.ContainsKey("netcoreapp1.0"));
                Assert.True(PackageXReferences["netcoreapp1.0"].Where(p => p.Item1.Equals(packageX.Id)).Count() == 1);
                Assert.True(PackageYReferences.ContainsKey("netstandard1.3"));
                Assert.True(PackageYReferences["netstandard1.3"].Where(p => p.Item1.Equals(packageY.Id)).Count() == 1);
            }
        }
    }
}
