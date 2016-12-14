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
using NuGet.Frameworks;
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

        [Fact]
        public static void AddPkg_ArgParsing()
        {
            // Arrange
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            var package = "package_foo";
            var version = "1.0.0-foo";
            var dotnet = "dotnet_foo";
            var project = "project_foo";

            var args = new List<string>() {
                "addpkg",
                "--package",
                package,
                "--version",
                version,
                "--dotnet",
                dotnet,
                "--project",
                project};

            var log = new TestCommandOutputLogger();
            var testApp = new CommandLineApplication();
            testApp.Name = "dotnet nuget_test";

            var mockCommandRunner = new Mock<IAddPackageReferenceCommandRunner>();
            mockCommandRunner
                .Setup(m => m.ExecuteCommand(It.IsAny<PackageReferenceArgs>(), It.IsAny<MSBuildAPIUtility>()))
                .Returns(0);

            AddPackageReferenceCommand.Register(testApp,
                () => log,
                () => mockCommandRunner.Object);

            testApp.OnExecute(() =>
            {
                testApp.ShowHelp();

                return 0;
            });

            // Act
            var exitCode = testApp.Execute(args.ToArray());

            // Assert
            mockCommandRunner.Verify(m => m.ExecuteCommand(It.Is<PackageReferenceArgs>(p => p.PackageIdentity.Id == package &&
            p.PackageIdentity.Version.ToNormalizedString() == version &&
            p.ProjectPath == project &&
            p.DotnetPath == dotnet &&
            !p.HasFrameworks &&
            !p.NoRestore),
            It.IsAny<MSBuildAPIUtility>()));

            Assert.Equal(exitCode, 0);
        }

        [Theory]
        [InlineData("addpkg --package Newtonsoft.json --version 9.0.1")]
        public static void AddPkg_UnconditionalAdd(string args)
        {
            // Arrange
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            Console.WriteLine("Waiting for debugger to attach.");
            Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");

            //while (!Debugger.IsAttached)
            //{
            //    System.Threading.Thread.Sleep(100);
            //}
            //Debugger.Break();

            var argBuilder = new StringBuilder(args);

            using (var pathContext = new SimpleTestPathContext())
            {
                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETCoreApp1.0"));

                projectA.Save();

                var dotnet = @"F:\paths\dotnet\dotnet.exe";

                argBuilder.Append($" --dotnet {dotnet} --project {projectA.ProjectPath}");

                // Act
                var result = CommandRunner.Run(
                      DotnetCli,
                      pathContext.WorkingDirectory,
                      $"{XplatDll} {argBuilder.ToString()}",
                      waitForExit: true);

                // Assert
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