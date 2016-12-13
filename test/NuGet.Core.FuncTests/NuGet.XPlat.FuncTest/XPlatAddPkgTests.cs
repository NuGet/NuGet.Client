// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Xml;
using System.Xml.Linq;
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
        public static void Locals_List_Succeeds(string args)
        {
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                // Arrange
                var testCSProjPath = CreateTestProject(mockBaseDirectory);

                // Act
                var result = CommandRunner.Run(
                      DotnetCli,
                      Directory.GetCurrentDirectory(),
                      $"{XplatDll} {args}",
                      waitForExit: true);

                // Assert
                var projectXml = LoadCSProj(testCSProjPath);
            }
        }

        private static string CreateTestProject(TestDirectory mockDirectory)
        {
            // Create new dotnet project
            var createProject = CommandRunner.Run(
                  DotnetCli,
                  mockDirectory,
                  $"{XplatDll} new",
                  waitForExit: true);

            Assert.True(File.Exists(Path.Combine(mockDirectory, mockDirectory.Info.Name, @".csproj")));

            return Path.Combine(mockDirectory, mockDirectory.Info.Name, @".csproj");
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