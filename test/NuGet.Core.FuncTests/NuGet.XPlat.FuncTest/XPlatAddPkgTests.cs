// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatAddPkgTests
    {
        private static readonly string DotnetCli = DotnetCliUtil.GetDotnetCli(getLatestCli: true);
        private static readonly string XplatDll = DotnetCliUtil.GetXplatDll();
        private static readonly string projectName = "test_project_addpkg";

        // Argument parsing related tests

        [Theory]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "--dotnet", "dotnet_foo", "--project", "project_foo", "", "", "", "", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "", "", "", "", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "--frameworks", "net46;netcoreapp1.0", "", "", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "-f", "net46 ; netcoreapp1.0 ; ", "", "", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "-f", "net46", "", "", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "", "", "--sources", "a;b", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "", "", "-s", "a ; b ;", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "", "", "-s", "a", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "", "", "", "", "--package-directory", @"foo\dir", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "", "", "", "", "", "", "--no-restore")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "", "", "", "", "", "", "-n")]
        public void AddPkg_ArgParsing(string packageOption, string package, string versionOption, string version, string dotnetOption,
        string dotnet, string projectOption, string project, string frameworkOption, string frameworkString, string sourceOption,
        string sourceString, string packageDirectoryOption, string packageDirectory, string noRestoreSwitch)
        {
            // Arrange
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            var argList = new List<string>() {
                "add",
                packageOption,
                package,
                versionOption,
                version,
                dotnetOption,
                dotnet,
                projectOption,
                project};

            if (!string.IsNullOrEmpty(frameworkOption))
            {
                argList.Add(frameworkOption);
                argList.Add(frameworkString);
            }
            if (!string.IsNullOrEmpty(sourceOption))
            {
                argList.Add(sourceOption);
                argList.Add(sourceString);
            }
            if (!string.IsNullOrEmpty(packageDirectoryOption))
            {
                argList.Add(packageDirectoryOption);
                argList.Add(packageDirectory);
            }
            if (!string.IsNullOrEmpty(noRestoreSwitch))
            {
                argList.Add(noRestoreSwitch);
            }

            var logger = new TestCommandOutputLogger();
            var testApp = new CommandLineApplication();
            var mockCommandRunner = new Mock<IAddPackageReferenceCommandRunner>();
            mockCommandRunner
                .Setup(m => m.ExecuteCommand(It.IsAny<PackageReferenceArgs>(), It.IsAny<MSBuildAPIUtility>()))
                .Returns(0);

            testApp.Name = "dotnet nuget_test";
            AddPackageReferenceCommand.Register(testApp,
                () => logger,
                () => mockCommandRunner.Object);

            // Act
            var result = testApp.Execute(argList.ToArray());

            // Assert
            mockCommandRunner.Verify(m => m.ExecuteCommand(It.Is<PackageReferenceArgs>(p => p.PackageDependency.Id == package &&
            p.PackageDependency.VersionRange.OriginalString == version &&
            p.ProjectPath == project &&
            p.DotnetPath == dotnet &&
            p.NoRestore == !string.IsNullOrEmpty(noRestoreSwitch) &&
            (string.IsNullOrEmpty(frameworkOption) || !string.IsNullOrEmpty(frameworkOption) && p.Frameworks.SequenceEqual(StringUtility.Split(frameworkString))) &&
            (string.IsNullOrEmpty(sourceOption) || !string.IsNullOrEmpty(sourceOption) && p.Sources.SequenceEqual(StringUtility.Split(sourceString))) &&
            (string.IsNullOrEmpty(packageDirectoryOption) || !string.IsNullOrEmpty(packageDirectoryOption) && p.PackageDirectory == packageDirectory)),
            It.IsAny<MSBuildAPIUtility>()));

            Assert.Equal(0, result);
        }

        // Add Related Tests

        [Theory]
        [InlineData("1.0.0")]
        [InlineData("*")]
        [InlineData("1.*")]
        [InlineData("1.0.*")]
        public async void AddPkg_UnconditionalAdd_Success(string userInputVersion)
        {
            // Arrange
            AssertDotnetAndXPlatPaths();

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46");
                var packageX = CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersion, projectA.ProjectPath);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForAllFrameworks(projectXmlRoot);

                // Assert
                Assert.Equal(0, result);
                Assert.NotNull(itemGroup);
                Assert.True(ValidateReference(itemGroup, packageX.Id, userInputVersion));
            }
        }

        [Theory]
        [InlineData("net46", "net46; netcoreapp1.0", "1.*")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "1.*")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "1.*")]
        [InlineData("net46", "net46; netcoreapp1.0", "1.*")]
        [InlineData("netcoreapp1.0", "net46; netcoreapp1.0", "1.*")]
        public async void AddPkg_UnconditionalAddWithNoRestore_Success(string packageFrameworks,
            string projectFrameworks,
            string userInputVersion)
        {
            // Arrange
            AssertDotnetAndXPlatPaths();

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, projectFrameworks);
                var packageX = CreatePackage(frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersion, projectA.ProjectPath, noRestore: true);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // If noRestore is set, then we do not perform compatibility check.
                // The added package reference will be unconditional
                var itemGroup = GetItemGroupForAllFrameworks(projectXmlRoot);

                // Assert
                Assert.Equal(0, result);
                Assert.NotNull(itemGroup);
                Assert.True(ValidateReference(itemGroup, packageX.Id, userInputVersion));
            }
        }

        [Fact]
        public async void AddPkg_UnconditionalAddWithoutVersion_Success()
        {
            // Arrange
            AssertDotnetAndXPlatPaths();

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46");
                var packageX = CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Since user is not inputing a version, it is converted to a "*"
                var packageArgs = GetPackageReferenceArgs(packageX.Id, "*", projectA.ProjectPath, noVersion: true);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                Assert.Equal(0, result);

                // Since user did not specify a version, the package reference will contain the resolved version
                Assert.True(ValidateReference(projectXmlRoot, packageX.Id, "1.0.0"));
            }
        }

        [Theory]
        [InlineData("net46", "net46; netcoreapp1.0", "1.0.0")]
        [InlineData("net46", "net46; netcoreapp1.0", "*")]
        public async void AddPkg_ConditionalAddWithoutUserInputFramework_Success(string packageFrameworks,
            string projectFrameworks, string userInputVersion)
        {
            // Arrange
            AssertDotnetAndXPlatPaths();
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, projectFrameworks);
                var packageX = CreatePackage(frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersion, projectA.ProjectPath);
                var commandRunner = new AddPackageReferenceCommandRunner();
                var commonFramework = GetCommonFramework(packageFrameworks, projectFrameworks);

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForFramework(projectXmlRoot, commonFramework);

                // Assert
                Assert.Equal(0, result);
                Assert.NotNull(itemGroup);
                Assert.True(ValidateReference(itemGroup, packageX.Id, userInputVersion));
            }
        }

        [Theory]
        [InlineData("net46", "net46; netcoreapp1.0", "net46")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "net46")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "netcoreapp1.0")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46; netcoreapp1.0")]
        [InlineData("netcoreapp1.0", "net46; netcoreapp1.0", "net46; netcoreapp1.0")]
        public async void AddPkg_ConditionalAddWithUserInputFramework_Success(string packageFrameworks, string projectFrameworks, string userInputFrameworks)
        {
            // Arrange
            AssertDotnetAndXPlatPaths();
            //WaitForDebugger();
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46; netcoreapp1.0");
                var packageX = CreatePackage(frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, packageX.Version, projectA.ProjectPath,
                    frameworks: userInputFrameworks);
                var commandRunner = new AddPackageReferenceCommandRunner();
                var commonFramework = GetCommonFramework(packageFrameworks, projectFrameworks, userInputFrameworks);

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForFramework(projectXmlRoot, commonFramework);

                // Assert
                Assert.Equal(0, result);
                Assert.NotNull(itemGroup);
                Assert.True(ValidateReference(itemGroup, packageX.Id, packageX.Version));
            }
        }

        [Theory]
        [InlineData("net46", "net46; netcoreapp1.0", "net46")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "net46")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "netcoreapp1.0")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46; netcoreapp1.0")]
        [InlineData("netcoreapp1.0", "net46; netcoreapp1.0", "net46; netcoreapp1.0")]
        public async void AddPkg_ConditionalAddWithoutVersion_Success(string packageFrameworks,
            string projectFrameworks,
            string userInputFrameworks)
        {
            // Arrange
            AssertDotnetAndXPlatPaths();

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, projectFrameworks);
                var packageX = CreatePackage(frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Since user is not inputing a version, it is converted to a "*" in the command
                var packageArgs = GetPackageReferenceArgs(packageX.Id, "*",
                    projectA.ProjectPath,
                    frameworks: userInputFrameworks,
                    noVersion: true);

                var commandRunner = new AddPackageReferenceCommandRunner();
                var commonFramework = GetCommonFramework(packageFrameworks, projectFrameworks, userInputFrameworks);

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForFramework(projectXmlRoot, commonFramework);

                // Assert
                Assert.Equal(0, result);
                Assert.NotNull(itemGroup);

                // Since user did not specify a version, the package reference will contain the resolved version
                Assert.True(ValidateReference(itemGroup, packageX.Id, "1.0.0"));
            }
        }

        [Theory]
        [InlineData("net46", "netcoreapp1.0")]
        [InlineData("netcoreapp1.0", "net46")]
        [InlineData("net46", "unknown_framework")]
        [InlineData("netcoreapp1.0", "unknown_framework")]
        [InlineData("net46; netcoreapp1.0", "unknown_framework")]
        public async void AddPkg_Failure(string packageFrameworks, string userInputFrameworks)
        {
            // Arrange
            AssertDotnetAndXPlatPaths();

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46; netcoreapp1.0");
                var packageX = CreatePackage(frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, packageX.Version, projectA.ProjectPath,
                    frameworks: userInputFrameworks);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                Assert.Equal(1, result);
                Assert.True(ValidateNoReference(projectXmlRoot, packageX.Id));
            }
        }

        [Fact]
        public async void AddPkg_UnconditionalAddTwoPackages_Success()
        {
            // Arrange
            AssertDotnetAndXPlatPaths();

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46");
                var packageX = CreatePackage("PkgX");
                var packageY = CreatePackage("PkgY");

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageY);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, packageX.Version, projectA.ProjectPath);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                packageArgs = GetPackageReferenceArgs(packageY.Id, packageY.Version, projectA.ProjectPath);

                // Act
                result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                Assert.Equal(0, result);
                Assert.True(ValidateTwoReferences(projectXmlRoot, packageX, packageY));
            }
        }

        [Theory]
        [InlineData("net46", "net46; netcoreapp1.0", "net46")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "net46")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "netcoreapp1.0")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46; netcoreapp1.0")]
        [InlineData("netcoreapp1.0", "net46; netcoreapp1.0", "net46; netcoreapp1.0")]
        public async void AddPkg_ConditionalAddTwoPackages_Success(string packageFrameworks, string projectFrameworks, string userInputFrameworks)
        {
            // Arrange
            AssertDotnetAndXPlatPaths();
            //WaitForDebugger();
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, projectFrameworks);
                var packageX = CreatePackage("PkgX", frameworkString: packageFrameworks);
                var packageY = CreatePackage("PkgY", frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageY);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, packageX.Version, projectA.ProjectPath, frameworks: userInputFrameworks);
                var commandRunner = new AddPackageReferenceCommandRunner();
                var commonFramework = GetCommonFramework(packageFrameworks, projectFrameworks, userInputFrameworks);

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                packageArgs = GetPackageReferenceArgs(packageY.Id, packageY.Version, projectA.ProjectPath);

                // Act
                result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForFramework(projectXmlRoot, commonFramework);

                // Assert
                Assert.Equal(0, result);
                Assert.True(ValidateTwoReferences(projectXmlRoot, packageX, packageY));
            }
        }

        // Update Related Tests

        [Theory]
        [InlineData("0.0.5", "1.0.0")]
        [InlineData("0.0.5", "0.9")]
        [InlineData("0.0.5", "*")]
        [InlineData("*", "1.0.0")]
        [InlineData("*", "0.9")]
        [InlineData("*", "1.*")]
        public async void AddPkg_UnconditionalAddAsUpdate_Succcess(string userInputVersionOld, string userInputVersionNew)
        {
            // Arrange
            AssertDotnetAndXPlatPaths();

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46; netcoreapp1.0");
                var packageX = CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersionOld, projectA.ProjectPath);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Create a package ref with the old version
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersionNew, projectA.ProjectPath);
                commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                // Create a package ref with the new version
                result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                // Verify that the only package reference is with the new version
                Assert.Equal(0, result);
                Assert.True(ValidateReference(projectXmlRoot, packageX.Id, userInputVersionNew));
            }
        }

        [Theory]
        [InlineData("net46", "net46; netcoreapp1.0", "net46", "0.0.5", "1.0.0")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "net46", "0.0.5", "1.0.0")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "netcoreapp1.0", "0.0.5", "1.0.0")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "0.0.5", "1.0.0")]
        [InlineData("netcoreapp1.0", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "0.0.5", "1.0.0")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46", "0.0.5", "0.9")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "net46", "0.0.5", "0.9")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "netcoreapp1.0", "0.0.5", "0.9")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "0.0.5", "0.9")]
        [InlineData("netcoreapp1.0", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "0.0.5", "0.9")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46", "0.0.5", "*")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "net46", "0.0.5", "*")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "netcoreapp1.0", "0.0.5", "*")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "0.0.5", "*")]
        [InlineData("netcoreapp1.0", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "0.0.5", "*")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46", "*", "1.0.0")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "net46", "*", "1.0.0")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "netcoreapp1.0", "*", "1.0.0")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "*", "1.0.0")]
        [InlineData("netcoreapp1.0", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "*", "1.0.0")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46", "*", "0.9")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "net46", "*", "0.9")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "netcoreapp1.0", "*", "0.9")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "*", "0.9")]
        [InlineData("netcoreapp1.0", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "*", "0.9")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46", "*", "1.*")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "net46", "*", "1.*")]
        [InlineData("net46; netcoreapp1.0", "net46; netcoreapp1.0", "netcoreapp1.0", "*", "1.*")]
        [InlineData("net46", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "*", "1.*")]
        [InlineData("netcoreapp1.0", "net46; netcoreapp1.0", "net46; netcoreapp1.0", "*", "1.*")]
        public async void AddPkg_ConditionalAddAsUpdate_Succcess(string packageFrameworks, string projectFrameworks,
            string userInputFrameworks, string userInputVersionOld, string userInputVersionNew)
        {
            // Arrange
            AssertDotnetAndXPlatPaths();

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, projectFrameworks);
                var packageX = CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersionOld, projectA.ProjectPath);
                var commandRunner = new AddPackageReferenceCommandRunner();
                var msBuild = new MSBuildAPIUtility();

                // Create a package ref with old version
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility());
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersionNew, projectA.ProjectPath);
                commandRunner = new AddPackageReferenceCommandRunner();
                var commonFramework = GetCommonFramework(packageFrameworks, projectFrameworks, userInputFrameworks);

                // Act
                // Create a package ref with new version
                result = commandRunner.ExecuteCommand(packageArgs, msBuild);
                projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                // Verify that the only package reference is with the new version
                Assert.Equal(0, result);
                Assert.True(ValidateReference(projectXmlRoot, packageX.Id, userInputVersionNew));
            }
        }

        // Helper Methods

        // Arrange Helper Methods
        private SimpleTestProjectContext CreateProject(string projectName, SimpleTestPathContext pathContext, string projectFrameworks)
        {
            var projectFrameworkList = new List<NuGetFramework>();
            StringUtility.Split(projectFrameworks)
                .ToList()
                .ForEach(f => projectFrameworkList.Add(NuGetFramework.Parse(f)));

            var project = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName: projectName,
                    solutionRoot: pathContext.SolutionRoot,
                    isToolingVersion15: true,
                    frameworks: projectFrameworkList.ToArray());

            project.Save();
            return project;
        }

        private SimpleTestPackageContext CreatePackage(string packageId = "packageX", string packageVersion = "1.0.0", string frameworkString = null)
        {
            var package = new SimpleTestPackageContext()
            {
                Id = packageId,
                Version = packageVersion
            };
            var frameworks = StringUtility.Split(frameworkString);

            // Make the package Compatible with specific frameworks
            frameworks?
                .ToList()
                .ForEach(f => package.AddFile($"lib/{f}/a.dll"));

            // To ensure that the nuspec does not have System.Runtime.dll
            package.Nuspec = GetNetCoreNuspec(packageId, packageVersion);

            return package;
        }

        private PackageReferenceArgs GetPackageReferenceArgs(string packageId, string packageVersion, string projectPath,
            string frameworks = "", string packageDirectory = "", string sources = "", bool noRestore = false, bool noVersion = false)
        {
            var logger = new TestCommandOutputLogger();
            var packageDependency = new PackageDependency(packageId, VersionRange.Parse(packageVersion));

            return new PackageReferenceArgs(DotnetCli, projectPath, packageDependency, logger)
            {
                Frameworks = StringUtility.Split(frameworks),
                Sources = StringUtility.Split(sources),
                PackageDirectory = packageDirectory,
                NoRestore = noRestore,
                NoVersion = noVersion
            };
        }

        private string GetCommonFramework(string frameworkStringA, string frameworkStringB, string frameworkStringC)
        {
            var frameworksA = StringUtility.Split(frameworkStringA);
            var frameworksB = StringUtility.Split(frameworkStringB);
            var frameworksC = StringUtility.Split(frameworkStringC);
            return frameworksA.ToList()
                .Intersect(frameworksB.ToList())
                .Intersect(frameworksC.ToList())
                .First();
        }

        private string GetCommonFramework(string frameworkStringA, string frameworkStringB)
        {
            var frameworksA = StringUtility.Split(frameworkStringA);
            var frameworksB = StringUtility.Split(frameworkStringB);
            return frameworksA.ToList()
                .Intersect(frameworksB.ToList())
                .First();
        }

        private XDocument GetNetCoreNuspec(string package, string packageVersion)
        {
            return XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>{package}</id>
                            <version>{packageVersion}</version>
                            <title />
                        </metadata>
                        </package>");
        }

        // Assert Helper Methods

        private bool ValidateReference(XElement root, string packageId, string version)
        {
            var packageReferences = root
                    .Descendants("PackageReference")
                    .Where(d => d.FirstAttribute.Value.Equals(packageId, StringComparison.OrdinalIgnoreCase));

            if (packageReferences.Count() != 1)
            {
                return false;
            }

            var versions = packageReferences
                .First()
                .Descendants("Version");

            if (versions.Count() != 1 ||
                !versions.First().Value.Equals(version, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }

        private bool ValidateTwoReferences(XElement root, SimpleTestPackageContext packageX, SimpleTestPackageContext packageY)
        {
            return ValidateReference(root, packageX.Id, packageX.Version) &&
                ValidateReference(root, packageY.Id, packageY.Version);
        }

        private bool ValidateNoReference(XElement root, string packageId)
        {
            var packageReferences = root
                    .Descendants("PackageReference")
                    .Where(d => d.FirstAttribute.Value.Equals(packageId, StringComparison.OrdinalIgnoreCase));

            if (packageReferences.Count() > 0)
            {
                return false;
            }
            return true;
        }

        private XElement GetItemGroupForFramework(XElement root, string framework)
        {
            var itemGroups = root.Descendants("ItemGroup");

            return itemGroups
                    .Where(i => i.Descendants("PackageReference").Count() > 0 &&
                                i.FirstAttribute != null &&
                                i.FirstAttribute.Name.LocalName.Equals("Condition", StringComparison.OrdinalIgnoreCase) &&
                                i.FirstAttribute.Value.Equals(GetTargetFrameworkCondition(framework), StringComparison.OrdinalIgnoreCase))
                     .First();
        }

        private XElement GetItemGroupForAllFrameworks(XElement root)
        {
            var itemGroups = root.Descendants("ItemGroup");

            return itemGroups
                    .Where(i => i.Descendants("PackageReference").Count() > 0 &&
                                i.FirstAttribute == null)
                     .First();
        }

        private XDocument LoadCSProj(string path)
        {
            return XPlatTestUtils.LoadSafe(path);
        }

        private string GetTargetFrameworkCondition(string targetFramework)
        {
            return string.Format("'$(TargetFramework)' == '{0}'", targetFramework);
        }

        private void AssertDotnetAndXPlatPaths()
        {
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);
        }
    }
}