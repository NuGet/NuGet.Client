// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
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

        [Theory]
        [InlineData("PkgX", "1.0.0", "1.0.0")]
        [InlineData("PkgX", "1.0.0", "*")]
        [InlineData("PkgX", "1.0.0", "1.*")]
        [InlineData("PkgX", "1.0.0", "1.0.*")]
        public async void AddPkg_UnconditionalAdd_Success(string package, string packageVersion, string userInputVersion)
        {
            // Arrange
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            var projectName = "test_project_a";
            var frameworks = "";
            var sources = "";
            var packageDirectory = "";
            var noRestore = false;

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName,
                    pathContext.SolutionRoot,
                    true,
                    NuGetFramework.Parse("netcoreapp1.0"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = package,
                    Version = packageVersion
                };
                projectA.Save();

                var dotnet = DotnetCli;
                var project = projectA.ProjectPath;

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var argList = new List<string>() {
                    "addpkg",
                    "--package",
                    package,
                    "--version",
                    userInputVersion,
                    "--dotnet",
                    dotnet,
                    "--project",
                    project };

                var logger = new TestCommandOutputLogger();
                var packageDependency = new PackageDependency(package, VersionRange.Parse(userInputVersion));
                var packageArgs = new PackageReferenceArgs(dotnet, project, packageDependency, logger)
                {
                    Frameworks = StringUtility.Split(frameworks),
                    Sources = StringUtility.Split(sources),
                    PackageDirectory = packageDirectory,
                    NoRestore = noRestore
                };
                var commandRunner = new AddPackageReferenceCommandRunner();
                var msBuild = new MSBuildAPIUtility();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, msBuild);
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                Assert.Equal(0, result);
                Assert.True(ValidateReference(projectXmlRoot, package, userInputVersion));
            }
        }

        [Theory]
        [InlineData("PkgX", "1.0.0", "1.0.0", "net46")]
        [InlineData("PkgX", "1.0.0", "*", "net46")]
        [InlineData("PkgX", "1.0.0", "1.*", "net46")]
        [InlineData("PkgX", "1.0.0", "1.0.*", "net46")]
        [InlineData("PkgX", "1.0.0", "1.0.0", "netcoreapp1.0")]
        [InlineData("PkgX", "1.0.0", "*", "netcoreapp1.0")]
        [InlineData("PkgX", "1.0.0", "1.*", "netcoreapp1.0")]
        [InlineData("PkgX", "1.0.0", "1.0.*", "netcoreapp1.0")]
        public async void AddPkg_ConditionalAddWithoutFramework_Success(string package, string packageVersion, string userInputVersion, string packageFramework)
        {
            // Arrange
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            var projectName = "test_project_a";
            var frameworks = "";
            var sources = "";
            var packageDirectory = "";
            var noRestore = false;

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName,
                    pathContext.SolutionRoot,
                    true,
                    NuGetFramework.Parse("netcoreapp1.0"),
                    NuGetFramework.Parse("net46"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = package,
                    Version = packageVersion
                };

                // Make package compatible only with net46
                packageX.AddFile($"lib/{packageFramework}/a.dll");

                packageX.Nuspec = GetNetCoreNuspec(package, packageVersion);

                projectA.Save();

                var dotnet = DotnetCli;
                var project = projectA.ProjectPath;

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var argList = new List<string>() {
                    "addpkg",
                    "--package",
                    package,
                    "--version",
                    userInputVersion,
                    "--dotnet",
                    dotnet,
                    "--project",
                    project };

                var logger = new TestCommandOutputLogger();
                var packageDependency = new PackageDependency(package, VersionRange.Parse(userInputVersion));
                var packageArgs = new PackageReferenceArgs(dotnet, project, packageDependency, logger)
                {
                    Frameworks = StringUtility.Split(frameworks),
                    Sources = StringUtility.Split(sources),
                    PackageDirectory = packageDirectory,
                    NoRestore = noRestore
                };
                var commandRunner = new AddPackageReferenceCommandRunner();
                var msBuild = new MSBuildAPIUtility();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, msBuild);
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForFramework(projectXmlRoot, packageFramework);
                // Assert

                Assert.Equal(0, result);
                Assert.NotNull(itemGroup);
                Assert.True(ValidateReference(itemGroup, package, userInputVersion));
            }
        }

        [Theory]
        [InlineData("PkgX", "1.0.0", "1.0.0", "net46", "net46")]
        [InlineData("PkgX", "1.0.0", "1.0.0", "netcoreapp1.0", "netcoreapp1.0")]
        public async void AddPkg_ConditionalAddWithFramework_Success(string package, string packageVersion, string userInputVersion, string packageFramework, string userFramework)
        {
            // Arrange
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            var projectName = "test_project_a";
            var frameworks = "";
            var sources = "";
            var packageDirectory = "";
            var noRestore = false;

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName,
                    pathContext.SolutionRoot,
                    true,
                    NuGetFramework.Parse("netcoreapp1.0"),
                    NuGetFramework.Parse("net46"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = package,
                    Version = packageVersion
                };

                // Make package compatible only with net46
                packageX.AddFile($"lib/{packageFramework}/a.dll");

                packageX.Nuspec = GetNetCoreNuspec(package, packageVersion);

                projectA.Save();

                var dotnet = DotnetCli;
                var project = projectA.ProjectPath;

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var argList = new List<string>() {
                    "addpkg",
                    "--package",
                    package,
                    "--version",
                    userInputVersion,
                    "--dotnet",
                    dotnet,
                    "--project",
                    project ,
                    "--frameworks",
                    userFramework};

                var logger = new TestCommandOutputLogger();
                var packageDependency = new PackageDependency(package, VersionRange.Parse(userInputVersion));
                var packageArgs = new PackageReferenceArgs(dotnet, project, packageDependency, logger)
                {
                    Frameworks = StringUtility.Split(frameworks),
                    Sources = StringUtility.Split(sources),
                    PackageDirectory = packageDirectory,
                    NoRestore = noRestore
                };
                var commandRunner = new AddPackageReferenceCommandRunner();
                var msBuild = new MSBuildAPIUtility();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, msBuild);
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;
                var itemGroup = GetItemGroupForFramework(projectXmlRoot, userFramework);

                // Assert
                Assert.Equal(0, result);
                Assert.NotNull(itemGroup);
                Assert.True(ValidateReference(itemGroup, package, userInputVersion));
            }
        }

        [Theory]
        [InlineData("PkgX", "1.0.0", "net46", "netcoreapp1.0")]
        [InlineData("PkgX", "1.0.0", "netcoreapp1.0", "net46")]
        public async void AddPkg_ConditionalAddWithFramework_Failure(string package, string packageVersion, string packageFramework, string userFramework)
        {
            // Arrange
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            var projectName = "test_project_a";
            var frameworks = userFramework;
            var sources = "";
            var packageDirectory = "";
            var noRestore = false;

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName,
                    pathContext.SolutionRoot,
                    true,
                    NuGetFramework.Parse("netcoreapp1.0"),
                    NuGetFramework.Parse("net46"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = package,
                    Version = packageVersion
                };

                // Make package compatible only with net46
                packageX.AddFile($"lib/{packageFramework}/a.dll");

                packageX.Nuspec = GetNetCoreNuspec(package, packageVersion);

                projectA.Save();

                var dotnet = DotnetCli;
                var project = projectA.ProjectPath;

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var argList = new List<string>() {
                    "addpkg",
                    "--package",
                    package,
                    "--version",
                    packageVersion,
                    "--dotnet",
                    dotnet,
                    "--project",
                    project ,
                    "--frameworks",
                    userFramework};

                var logger = new TestCommandOutputLogger();
                var packageDependency = new PackageDependency(package, VersionRange.Parse(packageVersion));
                var packageArgs = new PackageReferenceArgs(dotnet, project, packageDependency, logger)
                {
                    Frameworks = StringUtility.Split(frameworks),
                    Sources = StringUtility.Split(sources),
                    PackageDirectory = packageDirectory,
                    NoRestore = noRestore
                };
                var commandRunner = new AddPackageReferenceCommandRunner();
                var msBuild = new MSBuildAPIUtility();

                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, msBuild);
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                Assert.Equal(1, result);
                Assert.True(ValidateNoReference(projectXmlRoot, package));
            }
        }

        [Theory]
        [InlineData("PkgX", "1.0.0", "0.0.5", "1.0.0")]
        [InlineData("PkgX", "1.0.0", "0.0.5", "0.9")]
        [InlineData("PkgX", "1.0.0", "*", "1.0.0")]
        [InlineData("PkgX", "1.0.0", "0.0.5", "*")]
        [InlineData("PkgX", "1.0.0", "0.0.5", "1.*")]
        [InlineData("PkgX", "1.0.0", "0.0.5", "1.0.*")]
        public async void AddPkg_UnconditionalAddAsUpdate_Succcess(string package, string packageVersion, string userInputVersionOld, string userInputVersionNew)
        {
            // Arrange
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            var projectName = "test_project_a";
            var frameworks = "";
            var sources = "";
            var packageDirectory = "";
            var noRestore = false;

            //WaitForDebugger();

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName,
                    pathContext.SolutionRoot,
                    true,
                    NuGetFramework.Parse("netcoreapp1.0"),
                    NuGetFramework.Parse("net46"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = package,
                    Version = packageVersion
                };

                packageX.Nuspec = GetNetCoreNuspec(package, packageVersion);

                projectA.Save();

                var dotnet = DotnetCli;
                var project = projectA.ProjectPath;

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var argList = new List<string>() {
                    "addpkg",
                    "--package",
                    package,
                    "--version",
                    userInputVersionOld,
                    "--dotnet",
                    dotnet,
                    "--project",
                    project
                    };

                var logger = new TestCommandOutputLogger();
                var packageDependency = new PackageDependency(package, VersionRange.Parse(userInputVersionOld));
                var packageArgs = new PackageReferenceArgs(dotnet, project, packageDependency, logger)
                {
                    Frameworks = StringUtility.Split(frameworks),
                    Sources = StringUtility.Split(sources),
                    PackageDirectory = packageDirectory,
                    NoRestore = noRestore
                };
                var commandRunner = new AddPackageReferenceCommandRunner();
                var msBuild = new MSBuildAPIUtility();

                // Create a package ref with version 0.0.5
                var result = commandRunner.ExecuteCommand(packageArgs, msBuild);
                var projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                argList = new List<string>() {
                    "addpkg",
                    "--package",
                    package,
                    "--version",
                    userInputVersionNew,
                    "--dotnet",
                    dotnet,
                    "--project",
                    project
                    };

                logger = new TestCommandOutputLogger();
                packageDependency = new PackageDependency(package, VersionRange.Parse(userInputVersionNew));
                packageArgs = new PackageReferenceArgs(dotnet, project, packageDependency, logger)
                {
                    Frameworks = StringUtility.Split(frameworks),
                    Sources = StringUtility.Split(sources),
                    PackageDirectory = packageDirectory,
                    NoRestore = noRestore
                };

                // Act
                // Create a package ref with version 1.0.0
                result = commandRunner.ExecuteCommand(packageArgs, msBuild);
                projectXmlRoot = LoadCSProj(projectA.ProjectPath).Root;

                // Assert
                // Verify that the only package reference is with 1.0.0
                Assert.Equal(0, result);
                Assert.True(ValidateReference(projectXmlRoot, package, userInputVersionNew));
            }
        }

        private XElement GetItemGroupForFramework(XElement root, string framework)
        {
            var itemGroups = root.Descendants("ItemGroup");

            return itemGroups
                    .Where(d => d.FirstAttribute != null &&
                                d.FirstAttribute.Name.LocalName.Equals("Condition", StringComparison.OrdinalIgnoreCase) &&
                                d.FirstAttribute.Value.Equals(GetTargetFrameworkCondition(framework), StringComparison.OrdinalIgnoreCase))
                     .First();
        }

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

        private XDocument LoadCSProj(string path)
        {
            return LoadSafe(path);
        }

        private XDocument LoadSafe(string filePath)
        {
            var settings = CreateSafeSettings();
            using (var reader = XmlReader.Create(filePath, settings))
            {
                return XDocument.Load(reader);
            }
        }

        private XmlReaderSettings CreateSafeSettings(bool ignoreWhiteSpace = false)
        {
            var safeSettings = new XmlReaderSettings
            {
#if !IS_CORECLR
                XmlResolver = null,
#endif
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = ignoreWhiteSpace
            };

            return safeSettings;
        }

        private string GetTargetFrameworkCondition(string targetFramework)
        {
            return string.Format("'$(TargetFramework)' == '{0}'", targetFramework);
        }

        private void WaitForDebugger()
        {
            Console.WriteLine("Waiting for debugger to attach.");
            Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");

            while (!Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(100);
            }
            Debugger.Break();
        }
    }
}