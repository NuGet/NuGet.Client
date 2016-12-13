// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatAddPkgTests
    {
        private static readonly string DotnetCli = DotnetCliUtil.GetDotnetCli();
        private static readonly string XplatDll = DotnetCliUtil.GetXplatDll();

        [Theory]
        [InlineData("addpkg --package Newtonsoft.json --version 9.0.1")]
        public static void AddPkg_Unconditional(string args)
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