// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Test.Utility;
using Xunit.Abstractions;

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

        /// <summary>
        /// Set by the MSTest framework. Used to write the MSBuild output into the test results
        /// and to attach the captured stdout/stderr as result files.
        /// </summary>
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task RestoreWithPackageReference_UsingVsUnderTestMSBuild_Succeeds()
        {
            // Arrange
            string? msbuildPath = LocateVisualStudioUnderTestMSBuild();
            if (msbuildPath is null)
            {
                Assert.Inconclusive(
                    "Could not locate MSBuild from the Visual Studio installation under test. " +
                    "Checked the 'MSBUILD_EXE_PATH' and 'VisualStudio.InstallationUnderTest.Path' environment variables, " +
                    "'VSAPPIDDIR'/'DevEnvDir', and vswhere. Ensure a Visual Studio instance with MSBuild is installed.");
            }

            using var pathContext = new SimpleTestPathContext();

            string packageName = "TestPackage";
            string packageVersion = "1.0.0";
            await CommonUtility.CreatePackageInSourceAsync(pathContext.PackageSource, packageName, packageVersion);

            string projectName = "test";
            string projectPath = Path.Combine(pathContext.SolutionRoot, projectName + ".csproj");
            File.WriteAllText(projectPath, GetProjectXml(packageName, packageVersion));

            // Act
            var outputCapture = new CapturingTestOutputHelper();
            CommandRunnerResult result = CommandRunner.Run(
                filename: msbuildPath!,
                workingDirectory: pathContext.SolutionRoot,
                arguments: $"-t:restore \"{projectPath}\"",
                testOutputHelper: outputCapture);

            CaptureMSBuildOutput(result, outputCapture);

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

        private void CaptureMSBuildOutput(CommandRunnerResult result, CapturingTestOutputHelper outputCapture)
        {
            // outputCapture preserves the chronological order in which MSBuild wrote to stdout and
            // stderr, because CommandRunner forwards both streams to the ITestOutputHelper as lines
            // arrive. result.AllOutput, by contrast, concatenates stdout then stderr and loses ordering.
            string combinedOutput = outputCapture.ToString();

            TestContext.WriteLine($"msbuild -t:restore exit code: {result.ExitCode}");
            TestContext.WriteLine(combinedOutput);

            string? resultsDirectory = TestContext.ResultsDirectory;
            if (string.IsNullOrEmpty(resultsDirectory))
            {
                // Without a results directory we can't attach a file, but the output above is still
                // captured inline in the test results.
                return;
            }

            // Write the captured output outside the SimpleTestPathContext working directory so the
            // file survives its disposal and can be copied into the test results.
            string outputDirectory = Path.Combine(resultsDirectory!, "MSBuildRestoreTestCase", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDirectory);

            string logPath = Path.Combine(outputDirectory, "msbuild.restore.log");
            File.WriteAllText(logPath, combinedOutput);

            TestContext.AddResultFile(logPath);
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
        /// Resolves the path to <c>MSBuild.exe</c> from the Visual Studio installation under test,
        /// without launching Visual Studio. Apex's own installation discovery (the internal
        /// <c>Microsoft.Test.Apex.VisualStudio.Skus</c> types) is not exposed as a public helper that
        /// can be queried without starting the host, so this mirrors the discovery Apex itself relies
        /// on and that the rest of this repository already uses (see
        /// <c>MsbuildIntegrationTestFixture</c> and <c>build/common.ps1</c>):
        /// <list type="number">
        /// <item><description><c>MSBUILD_EXE_PATH</c> (set by build.proj when running Apex tests standalone).</description></item>
        /// <item><description>The Apex install environment variables <c>VisualStudio.InstallationUnderTest.Path</c>,
        /// <c>VSAPPIDDIR</c>, and <c>DevEnvDir</c>.</description></item>
        /// <item><description><c>vswhere</c>, which uses the VS Setup Configuration API to locate an installed instance
        /// (this is what works on the CI/DartLab machines, where the install is configured via the .runsettings).</description></item>
        /// </list>
        /// </summary>
        /// <returns>The full path to MSBuild.exe, or <see langword="null" /> if it cannot be resolved.</returns>
        private static string? LocateVisualStudioUnderTestMSBuild()
        {
            // 1. MSBUILD_EXE_PATH points directly at MSBuild.exe when set (e.g. by build.proj).
            string? msbuildExePath = Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH");
            if (!string.IsNullOrEmpty(msbuildExePath) && File.Exists(msbuildExePath))
            {
                return msbuildExePath;
            }

            // 2. Apex install environment variables. The value may be the install root (build.proj sets
            // it to VSINSTALLDIR) or a path to devenv.exe (VisualStudioOperationsFixture sets it that way),
            // and VSAPPIDDIR/DevEnvDir point at <root>\Common7\IDE, so each value is normalized to a root.
            foreach (string variableName in new[] { "VisualStudio.InstallationUnderTest.Path", "VSAPPIDDIR", "DevEnvDir" })
            {
                string? installRoot = GetInstallRoot(Environment.GetEnvironmentVariable(variableName));
                string? msbuild = TryGetMSBuildFromInstallRoot(installRoot);
                if (msbuild != null)
                {
                    return msbuild;
                }
            }

            // 3. Fall back to vswhere, which is what locates the instance on CI machines.
            return FindMSBuildWithVsWhere();
        }

        /// <summary>
        /// Normalizes a Visual Studio path environment variable value to the installation root.
        /// Handles values that point at devenv.exe, at the <c>Common7\IDE</c> directory, or already at the root.
        /// </summary>
        private static string? GetInstallRoot(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            string path = value!;

            // A path to an executable (e.g. devenv.exe) -> its containing directory.
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                path = Path.GetDirectoryName(path) ?? path;
            }

            path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // <root>\Common7\IDE -> <root>
            string common7Ide = Path.Combine("Common7", "IDE");
            if (path.EndsWith(common7Ide, StringComparison.OrdinalIgnoreCase))
            {
                string? common7Directory = Path.GetDirectoryName(path);
                return common7Directory is null ? null : Path.GetDirectoryName(common7Directory);
            }

            return path;
        }

        /// <summary>
        /// Returns the path to <c>MSBuild.exe</c> under the given install root if it exists, otherwise <see langword="null" />.
        /// </summary>
        private static string? TryGetMSBuildFromInstallRoot(string? installRoot)
        {
            if (string.IsNullOrEmpty(installRoot))
            {
                return null;
            }

            string msbuildPath = Path.Combine(installRoot!, "MSBuild", "Current", "Bin", "MSBuild.exe");
            return File.Exists(msbuildPath) ? msbuildPath : null;
        }

        /// <summary>
        /// Locates MSBuild.exe using vswhere, which queries the Visual Studio Setup Configuration API.
        /// </summary>
        private static string? FindMSBuildWithVsWhere()
        {
            string vswherePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft Visual Studio",
                "Installer",
                "vswhere.exe");

            if (!File.Exists(vswherePath))
            {
                return null;
            }

            CommandRunnerResult result = CommandRunner.Run(
                filename: vswherePath,
                arguments: "-latest -prerelease -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe");

            if (!result.Success)
            {
                return null;
            }

            using var reader = new StringReader(result.Output);
            string? line = reader.ReadLine();

            return !string.IsNullOrEmpty(line) && File.Exists(line) ? line : null;
        }

        /// <summary>
        /// Captures the lines forwarded by <see cref="CommandRunner" /> into a single buffer,
        /// preserving the chronological order in which stdout and stderr were written.
        /// </summary>
        private sealed class CapturingTestOutputHelper : ITestOutputHelper
        {
            private readonly StringBuilder _builder = new();

            public void WriteLine(string message)
            {
                lock (_builder)
                {
                    _builder.AppendLine(message);
                }
            }

            public void WriteLine(string format, params object[] args)
            {
                WriteLine(string.Format(CultureInfo.CurrentCulture, format, args));
            }

            public override string ToString()
            {
                lock (_builder)
                {
                    return _builder.ToString();
                }
            }
        }
    }
}
