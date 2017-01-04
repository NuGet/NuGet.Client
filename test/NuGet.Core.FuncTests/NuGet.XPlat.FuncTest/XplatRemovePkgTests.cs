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
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatRemovePkgTests
    {
        private static readonly string projectName = "test_project_removepkg";

        // Argument parsing related tests

        [Theory]
        [InlineData("--package", "package_foo", "--project", "project_foo")]
        [InlineData("--package", "package_foo", "-p", "project_foo")]
        public void AddPkg_ArgParsing(string packageOption, string package, 
            string projectOption, string project)
        {
            // Arrange

            var argList = new List<string>() {
                "remove",
                packageOption,
                package,
                projectOption,
                project};

            var logger = new TestCommandOutputLogger();
            var testApp = new CommandLineApplication();
            var mockCommandRunner = new Mock<IPackageReferenceCommandRunner>();
            mockCommandRunner
                .Setup(m => m.ExecuteCommand(It.IsAny<PackageReferenceArgs>(), It.IsAny<MSBuildAPIUtility>()))
                .ReturnsAsync(0);

            testApp.Name = "dotnet nuget_test";
            RemovePackageReferenceCommand.Register(testApp,
                () => logger,
                () => mockCommandRunner.Object);

            // Act
            var result = testApp.Execute(argList.ToArray());

            // Assert
            mockCommandRunner.Verify(m => m.ExecuteCommand(It.Is<PackageReferenceArgs>(p => 
            p.PackageDependency.Id == package &&
            p.ProjectPath == project),
            It.IsAny<MSBuildAPIUtility>()));

            Assert.Equal(0, result);
        }

        // Remove Related Tests

        [Fact]
        public async void RemovePkg_UnconditionalRemove_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Generate Package
                var packageX = CreatePackage();
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var projectA = CreateProject(projectName, pathContext, packageX, "net46");

                // Verify that the package reference exists before removing.
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForAllFrameworks(projectXmlRoot);

                Assert.NotNull(itemGroup);
                Assert.True(ValidateReference(itemGroup, packageX.Id, "1.0.0"));


                var packageArgs = GetPackageReferenceArgs(packageX.Id, projectA);
                var commandRunner = new RemovePackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility()).Result;
                projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                Assert.Equal(0, result);
                Assert.False(ValidateReference(itemGroup, packageX.Id, "1.0.0"));
            }
        }

        [Theory]
        [InlineData("net46")]
        [InlineData("netcoreapp1.0")]
        public async void RemovePkg_ConditionalRemove_Success(string packageframework)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Generate Package
                var packageX = CreatePackage(frameworkString: packageframework);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var projectA = CreateProject(projectName, pathContext, packageX, "net46; netcoreapp1.0", packageframework);

                // Verify that the package reference exists before removing.
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForFramework(projectXmlRoot, packageframework);

                Assert.NotNull(itemGroup);
                Assert.True(ValidateReference(itemGroup, packageX.Id, "1.0.0"));


                var packageArgs = GetPackageReferenceArgs(packageX.Id, projectA);
                var commandRunner = new RemovePackageReferenceCommandRunner();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, new MSBuildAPIUtility()).Result;
                projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                itemGroup = GetItemGroupForFramework(projectXmlRoot, packageframework);

                // Assert
                Assert.Equal(0, result);
                Assert.Null(itemGroup);
                Assert.False(ValidateReference(itemGroup, packageX.Id, "1.0.0"));
            }
        }

        // Helper Methods

        // Arrange Helper Methods

        private SimpleTestProjectContext CreateProject(string projectName, 
            SimpleTestPathContext pathContext,
            SimpleTestPackageContext package,
            string projectFrameworks,
            string packageFramework = null)
        {
            var project = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName: projectName,
                    solutionRoot: pathContext.SolutionRoot,
                    isToolingVersion15: true,
                    frameworks: StringUtility.Split(projectFrameworks));

            if(packageFramework == null)
            {
                project.AddPackageToAllFrameworks(package);
            }
            else
            {
                project.AddPackageToFramework(packageFramework, package);
            }
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
            var frameworks = StringUtility.Split(frameworkString);

            // Make the package Compatible with specific frameworks
            frameworks?
                .ToList()
                .ForEach(f => package.AddFile($"lib/{f}/a.dll"));

            // To ensure that the nuspec does not have System.Runtime.dll
            package.Nuspec = GetNetCoreNuspec(packageId, packageVersion);

            return package;
        }

        private PackageReferenceArgs GetPackageReferenceArgs(string packageId, SimpleTestProjectContext project)
        {
            var logger = new TestCommandOutputLogger();
            var packageDependency = new PackageDependency(packageId);
            return new PackageReferenceArgs(project.ProjectPath, packageDependency, logger);
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