// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Common;
using NuGet.Configuration;
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
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "-d", "dotnet_foo", "-p", "project_foo", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "--dotnet", "dotnet_foo", "--project", "project_foo", "", "", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "--dotnet", "dotnet_foo", "--project", "project_foo", "--frameworks", "net46;netcoreapp1.0", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "--dotnet", "dotnet_foo", "--project", "project_foo", "--frameworks", "net46 ; netcoreapp1.0 ; ", "")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "--dotnet", "dotnet_foo", "--project", "project_foo", "", "", "--no-restore")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "--dotnet", "dotnet_foo", "--project", "project_foo", "--frameworks", "net46;netcoreapp1.0", "--no-restore")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "--dotnet", "dotnet_foo", "--project", "project_foo", "-f", "net46;netcoreapp1.0", "-n")]
        [InlineData("--package", "package_foo", "--version", "1.0.0-foo", "--dotnet", "dotnet_foo", "--project", "project_foo", "-f", "net46;netcoreapp1.0", "-n")]
        public void AddPkg_ArgParsing(string packageOption, string package, string versionOption, string version, string dotnetOption,
            string dotnet, string projectOption, string project, string frameworkOption, string frameworkString, string noRestoreSwitch)
        {
            // Arrange
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            var argList = new List<string>() {
                "addpkg",
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
            var exitCode = testApp.Execute(argList.ToArray());

            // Assert
            mockCommandRunner.Verify(m => m.ExecuteCommand(It.Is<PackageReferenceArgs>(p => p.PackageIdentity.Id == package &&
            p.PackageIdentity.Version.ToNormalizedString() == version &&
            p.ProjectPath == project &&
            p.DotnetPath == dotnet &&
            p.HasFrameworks == !string.IsNullOrEmpty(frameworkOption) &&
            p.NoRestore == !string.IsNullOrEmpty(noRestoreSwitch) &&
            (!p.HasFrameworks || p.Frameworks.SequenceEqual(StringUtility.Split(frameworkString)))),
            It.IsAny<MSBuildAPIUtility>()));

            Assert.Equal(exitCode, 0);
        }

        [Theory]
        [InlineData("PkgX", "1.0.0")]
        public async void AddPkg_UnconditionalAdd(string package, string version)
        {
            // Arrange
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            Console.WriteLine("Waiting for debugger to attach.");
            Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");

            while (!Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(100);
            }
            Debugger.Break();

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netcoreapp1.0"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = package,
                    Version = version
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
                    version,
                    "--dotnet",
                    dotnet,
                    "--project",
                    project };

                var logger = new TestCommandOutputLogger();
                var packageIdentity = new PackageIdentity(package, new NuGetVersion(version));
                var settings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                var packageArgs = new PackageReferenceArgs(dotnet, project, packageIdentity, settings, logger, noRestore: false);
                var commandRunner = new AddPackageReferenceCommandRunner();
                var msBuild = new MSBuildAPIUtility();
                // Act
                var result = commandRunner.ExecuteCommand(packageArgs, msBuild);

                // Assert
                Assert.Equal(result, 0);
                var projectXml = LoadCSProj(projectA.ProjectPath);
                var x = projectXml.Root;
            }
        }

        private static XDocument LoadCSProj(string path)
        {
            return LoadSafe(path);
        }

        private static XDocument LoadSafe(string filePath)
        {
            var settings = CreateSafeSettings();
            using (var reader = XmlReader.Create(filePath, settings))
            {
                return XDocument.Load(reader);
            }
        }

        private static XmlReaderSettings CreateSafeSettings(bool ignoreWhiteSpace = false)
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
    }
}