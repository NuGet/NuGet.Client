// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Commands;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatAddPkgTests
    {
        private static readonly string projectName = "test_project_addpkg";

        private static MSBuildAPIUtility MsBuild
        {
            get { return new MSBuildAPIUtility(new TestCommandOutputLogger()); }
        }

        // Argument parsing related tests

        [Theory]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "--dg-file", "dgfile_foo", "--project", "project_foo", "", "", "", "", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dgfile_foo", "-p", "project_foo", "", "", "", "", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dgfile_foo", "-p", "project_foo", "--framework", "net46;netcoreapp1.0", "", "", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dgfile_foo", "-p", "project_foo", "-f", "net46 ; netcoreapp1.0 ; ", "", "", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dgfile_foo", "-p", "project_foo", "-f", "net46", "", "", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dgfile_foo", "-p", "project_foo", "", "", "--source", "a;b", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dgfile_foo", "-p", "project_foo", "", "", "-s", "a ; b ;", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dgfile_foo", "-p", "project_foo", "", "", "-s", "a", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dgfile_foo", "-p", "project_foo", "", "", "", "", "--package-directory", @"foo\dir", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dgfile_foo", "-p", "project_foo", "", "", "", "", "", "", "--no-restore")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dgfile_foo", "-p", "project_foo", "", "", "", "", "", "", "-n")]
        public void AddPkg_ArgParsing(string packageOption, string package, string versionOption, string version, string dgFileOption,
        string dgFilePath, string projectOption, string project, string frameworkOption, string frameworkString, string sourceOption,
        string sourceString, string packageDirectoryOption, string packageDirectory, string noRestoreSwitch)
        {
            // Arrange

            var argList = new List<string>() {
                "add",
                packageOption,
                package,
                versionOption,
                version,
                dgFileOption,
                dgFilePath,
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
            var mockCommandRunner = new Mock<IPackageReferenceCommandRunner>();
            mockCommandRunner
                .Setup(m => m.ExecuteCommand(It.IsAny<PackageReferenceArgs>(), It.IsAny<MSBuildAPIUtility>()))
                .ReturnsAsync(0);

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
            p.DgFilePath == dgFilePath &&
            p.NoRestore == !string.IsNullOrEmpty(noRestoreSwitch) &&
            (string.IsNullOrEmpty(frameworkOption) || !string.IsNullOrEmpty(frameworkOption) && p.Frameworks.SequenceEqual(MSBuildStringUtility.Split(frameworkString))) &&
            (string.IsNullOrEmpty(sourceOption) || !string.IsNullOrEmpty(sourceOption) && p.Sources.SequenceEqual(MSBuildStringUtility.Split(sourceString))) &&
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

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46");
                var packageX = CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersion, projectA);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, MsBuild).Result;
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

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, projectFrameworks);
                var packageX = CreatePackage(frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersion, projectA, noRestore: true);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, MsBuild)
                    .Result;
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
                var packageArgs = GetPackageReferenceArgs(packageX.Id, "*", projectA, noVersion: true);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, MsBuild)
                    .Result;
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

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, projectFrameworks);
                var packageX = CreatePackage(frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersion, projectA);
                var commandRunner = new AddPackageReferenceCommandRunner();
                var commonFramework = GetCommonFramework(packageFrameworks, projectFrameworks);

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, MsBuild)
                    .Result;
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

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46; netcoreapp1.0");
                var packageX = CreatePackage(frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, packageX.Version, projectA,
                    frameworks: userInputFrameworks);
                var commandRunner = new AddPackageReferenceCommandRunner();
                var commonFramework = GetCommonFramework(packageFrameworks, projectFrameworks, userInputFrameworks);

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, MsBuild)
                    .Result;
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
                    projectA,
                    frameworks: userInputFrameworks,
                    noVersion: true);

                var commandRunner = new AddPackageReferenceCommandRunner();
                var commonFramework = GetCommonFramework(packageFrameworks, projectFrameworks, userInputFrameworks);

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, MsBuild)
                    .Result;
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
        public async void AddPkg_FailureIncompatibleFrameworks(string packageFrameworks, string userInputFrameworks)
        {
            // Arrange

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46; netcoreapp1.0");
                var packageX = CreatePackage(frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, packageX.Version, projectA,
                    frameworks: userInputFrameworks);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, MsBuild)
                    .Result;
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                Assert.Equal(1, result);
                Assert.True(ValidateNoReference(projectXmlRoot, packageX.Id));
            }
        }

        [Fact]
        public async void AddPkg_FailureUnknownPackage()
        {
            // Arrange

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46; netcoreapp1.0");
                var packageX = CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs("unknown_package_id", "1.0.0", projectA);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, MsBuild)
                    .Result;
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                Assert.Equal(1, result);
                Assert.True(ValidateNoReference(projectXmlRoot, packageX.Id));
                Assert.True(ValidateNoReference(projectXmlRoot, "unknown_package_id"));
            }
        }

        [Fact]
        public async void AddPkg_UnconditionalAddTwoPackages_Success()
        {
            // Arrange

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

                var packageArgs = GetPackageReferenceArgs(packageX.Id, packageX.Version, projectA);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, MsBuild)
                    .Result;
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                packageArgs = GetPackageReferenceArgs(packageY.Id, packageY.Version, projectA);

                // Act
                result = commandRunner.ExecuteCommand(packageArgs, MsBuild)
                    .Result;
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

                var packageArgs = GetPackageReferenceArgs(packageX.Id, packageX.Version, projectA, frameworks: userInputFrameworks);
                var commandRunner = new AddPackageReferenceCommandRunner();
                var commonFramework = GetCommonFramework(packageFrameworks, projectFrameworks, userInputFrameworks);
                var msBuild = MsBuild;

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, msBuild)
                    .Result;
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                packageArgs = GetPackageReferenceArgs(packageY.Id, packageY.Version, projectA);

                // Act
                result = commandRunner.ExecuteCommand(packageArgs, msBuild)
                    .Result;
                projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForFramework(projectXmlRoot, commonFramework);

                // Assert
                Assert.Equal(0, result);
                Assert.True(ValidateTwoReferences(projectXmlRoot, packageX, packageY));
            }
        }

        [Fact]
        public async void AddPkg_UnconditionalAddWithPackageDirectory_Success()
        {
            // Arrange

            using (var tempGlobalPackagesDirectory = TestDirectory.Create())
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46");
                var packageX = CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, packageX.Version, projectA,
                    packageDirectory: tempGlobalPackagesDirectory.Path);
                var commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, MsBuild)
                    .Result;
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForAllFrameworks(projectXmlRoot);

                // Assert
                Assert.Equal(0, result);
                Assert.NotNull(itemGroup);
                Assert.True(ValidateReference(itemGroup, packageX.Id, packageX.Version));

                // Since user provided packge directory, assert if package is present
                Assert.True(ValidatePackageDownload(tempGlobalPackagesDirectory.Path, packageX));
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

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, "net46; netcoreapp1.0");
                var packageX = CreatePackage();

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersionOld, projectA);
                var commandRunner = new AddPackageReferenceCommandRunner();
                var msBuild = MsBuild;

                // Create a package ref with the old version
                var result = commandRunner.ExecuteCommand(packageArgs, msBuild)
                    .Result;
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersionNew, projectA);
                commandRunner = new AddPackageReferenceCommandRunner();

                // Act
                // Create a package ref with the new version
                result = commandRunner.ExecuteCommand(packageArgs, msBuild)
                    .Result;
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

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = CreateProject(projectName, pathContext, projectFrameworks);
                var packageX = CreatePackage(frameworkString: packageFrameworks);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersionOld, projectA);
                var commandRunner = new AddPackageReferenceCommandRunner();
                var msBuild = MsBuild;

                // Create a package ref with old version
                var result = commandRunner.ExecuteCommand(packageArgs, msBuild)
                    .Result;
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                packageArgs = GetPackageReferenceArgs(packageX.Id, userInputVersionNew, projectA);
                commandRunner = new AddPackageReferenceCommandRunner();
                var commonFramework = GetCommonFramework(packageFrameworks, projectFrameworks, userInputFrameworks);

                // Act
                // Create a package ref with new version
                result = commandRunner.ExecuteCommand(packageArgs, msBuild)
                    .Result;
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
            var project = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName: projectName,
                    solutionRoot: pathContext.SolutionRoot,
                    isToolingVersion15: true,
                    frameworks: MSBuildStringUtility.Split(projectFrameworks));

            project.Save();
            return project;
        }

        private string CreateDGFileForProject(SimpleTestProjectContext project)
        {
            var dgSpec = new DependencyGraphSpec();
            var dgFilePath = Path.Combine(Directory.GetParent(project.ProjectPath).FullName, "temp.dg");
            dgSpec.AddRestore(project.ProjectName);
            dgSpec.AddProject(project.PackageSpec);
            dgSpec.Save(dgFilePath);
            return dgFilePath;
        }

        private SimpleTestPackageContext CreatePackage(string packageId = "packageX", string packageVersion = "1.0.0", string frameworkString = null)
        {
            var package = new SimpleTestPackageContext()
            {
                Id = packageId,
                Version = packageVersion
            };
            var frameworks = MSBuildStringUtility.Split(frameworkString);

            // Make the package Compatible with specific frameworks
            frameworks?
                .ToList()
                .ForEach(f => package.AddFile($"lib/{f}/a.dll"));

            // To ensure that the nuspec does not have System.Runtime.dll
            package.Nuspec = GetNetCoreNuspec(packageId, packageVersion);

            return package;
        }

        private PackageReferenceArgs GetPackageReferenceArgs(string packageId, string packageVersion, SimpleTestProjectContext project,
            string frameworks = "", string packageDirectory = "", string sources = "", bool noRestore = false, bool noVersion = false)
        {
            var logger = new TestCommandOutputLogger();
            var packageDependency = new PackageDependency(packageId, VersionRange.Parse(packageVersion));
            var dgFilePath = string.Empty;
            if (!noRestore)
            {
                dgFilePath = CreateDGFileForProject(project);
            }
            return new PackageReferenceArgs(project.ProjectPath, packageDependency, logger)
            {
                Frameworks = MSBuildStringUtility.Split(frameworks),
                Sources = MSBuildStringUtility.Split(sources),
                PackageDirectory = packageDirectory,
                NoRestore = noRestore,
                NoVersion = noVersion,
                DgFilePath = dgFilePath
            };
        }

        private string GetCommonFramework(string frameworkStringA, string frameworkStringB, string frameworkStringC)
        {
            var frameworksA = MSBuildStringUtility.Split(frameworkStringA);
            var frameworksB = MSBuildStringUtility.Split(frameworkStringB);
            var frameworksC = MSBuildStringUtility.Split(frameworkStringC);
            return frameworksA.ToList()
                .Intersect(frameworksB.ToList())
                .Intersect(frameworksC.ToList())
                .First();
        }

        private string GetCommonFramework(string frameworkStringA, string frameworkStringB)
        {
            var frameworksA = MSBuildStringUtility.Split(frameworkStringA);
            var frameworksB = MSBuildStringUtility.Split(frameworkStringB);
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

            return !(packageReferences.Count() > 0);
        }

        private bool ValidatePackageDownload(string packageDirectoryPath, SimpleTestPackageContext package)
        {
            return Directory.Exists(packageDirectoryPath) &&
                Directory.Exists(Path.Combine(packageDirectoryPath, package.Id.ToLower())) &&
                Directory.Exists(Path.Combine(packageDirectoryPath, package.Id.ToLower(), package.Version.ToLower())) &&
                Directory.EnumerateFiles(Path.Combine(packageDirectoryPath, package.Id.ToLower(), package.Version.ToLower())).Count() > 0;
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
    }
}