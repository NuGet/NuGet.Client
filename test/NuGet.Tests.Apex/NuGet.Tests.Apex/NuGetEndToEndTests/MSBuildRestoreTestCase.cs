// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Test.Utility;

namespace NuGet.Tests.Apex
{
    /// <summary>
    /// Verifies that NuGet restore works on the machine under test by running
    /// <c>msbuild -t:restore</c> using the MSBuild that ships with the Visual Studio
    /// installation under test. This means if you modify restore and run this test locally,
    /// it will not test your changes. Use MSBuild.Integration.Tests or unit tests to test
    /// other changes. This test is intended to catch issues that may be specific to the environment
    /// on the machine, such as upgrading packages to a version incompatible with MSBuild.
    /// </summary>
    [TestClass]
    public class MSBuildRestoreTestCase
    {
        private const int DefaultTimeout = 5 * 60 * 1000; // 5 minutes

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task RestoreWithPackageReference_UsingVsUnderTestMSBuild_Succeeds()
        {
            // Arrange
            string? msbuildPath = LocateVisualStudioUnderTestMSBuild();
            if (msbuildPath is null)
            {
                Assert.Inconclusive(
                    "Could not locate the Visual Studio installation under test. " +
                    "Set the 'VisualStudio.InstallationUnderTest.Path' environment variable, " +
                    "or run from a Developer Command Prompt/PowerShell so that VSAPPIDDIR or DevEnvDir is set.");
            }

            using var pathContext = new SimpleTestPathContext();

            string packageName = "TestPackage";
            string packageVersion = "1.0.0";
            await CommonUtility.CreatePackageInSourceAsync(pathContext.PackageSource, packageName, packageVersion);

            string projectName = "test";
            string projectPath = Path.Combine(pathContext.SolutionRoot, projectName + ".csproj");
            File.WriteAllText(projectPath, GetProjectXml(packageName, packageVersion));

            // Act
            CommandRunnerResult result = CommandRunner.Run(
                filename: msbuildPath!,
                workingDirectory: pathContext.SolutionRoot,
                arguments: $"-t:restore \"{projectPath}\"");

            // Assert
            Assert.AreEqual(
                0,
                result.ExitCode,
                $"msbuild -t:restore failed (exit code {result.ExitCode}).{Environment.NewLine}{result.AllOutput}");

            string assetsFilePath = Path.Combine(pathContext.SolutionRoot, "obj", "project.assets.json");
            Assert.IsTrue(
                File.Exists(assetsFilePath),
                $"Expected restore to generate '{assetsFilePath}'.{Environment.NewLine}{result.AllOutput}");

            string assetsFileContent = File.ReadAllText(assetsFilePath);
            StringAssert.Contains(
                assetsFileContent,
                packageName,
                $"Expected the assets file to reference '{packageName}'.{Environment.NewLine}{result.AllOutput}");
        }

        private static string GetProjectXml(string packageName, string packageVersion)
        {
            return $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""{packageName}"" Version=""{packageVersion}"" />
  </ItemGroup>
</Project>";
        }

        /// <summary>
        /// Resolves the path to <c>MSBuild.exe</c> from the Visual Studio installation under test.
        /// Uses the same environment variables that the Apex host uses to locate Visual Studio:
        /// <c>VisualStudio.InstallationUnderTest.Path</c> (path to devenv.exe), falling back to
        /// <c>VSAPPIDDIR</c> / <c>DevEnvDir</c> (the Common7\IDE directory).
        /// </summary>
        /// <returns>The full path to MSBuild.exe, or <see langword="null" /> if it cannot be resolved.</returns>
        private static string? LocateVisualStudioUnderTestMSBuild()
        {
            string? ideDirectory = null;

            string? devenvPath = Environment.GetEnvironmentVariable("VisualStudio.InstallationUnderTest.Path");
            if (!string.IsNullOrEmpty(devenvPath))
            {
                // The value points to devenv.exe, which lives in <root>\Common7\IDE.
                ideDirectory = Path.GetDirectoryName(devenvPath);
            }

            if (string.IsNullOrEmpty(ideDirectory))
            {
                ideDirectory = Environment.GetEnvironmentVariable("VSAPPIDDIR")
                    ?? Environment.GetEnvironmentVariable("DevEnvDir");
            }

            if (string.IsNullOrEmpty(ideDirectory))
            {
                return null;
            }

            // <root>\Common7\IDE -> <root>
            string? common7Directory = Path.GetDirectoryName(ideDirectory!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string? installRoot = common7Directory is null ? null : Path.GetDirectoryName(common7Directory);

            if (string.IsNullOrEmpty(installRoot))
            {
                return null;
            }

            string msbuildPath = Path.Combine(installRoot!, "MSBuild", "Current", "Bin", "MSBuild.exe");

            return File.Exists(msbuildPath) ? msbuildPath : null;
        }
    }
}
