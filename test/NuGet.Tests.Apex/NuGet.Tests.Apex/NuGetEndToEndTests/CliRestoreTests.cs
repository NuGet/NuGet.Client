// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit.Abstractions;

namespace NuGet.Tests.Apex
{
    /// <summary>
    /// These tests are intended to validate that restore works with the MSBuild when NuGet is installed in VS normally.
    /// MSBuild.Integration.Tests should be used for more exhaustive testing of restore with MSBuild.
    /// </summary>
    /// <remarks>
    /// These tests locate MSBuild.exe using vswhere.exe, so when running these tests locally, they will not take into
    /// account any changes you have made to any product code. It will only test the NuGet that is installed in the VS
    /// installation directory.
    /// </remarks>
    [TestClass]
    public class CliRestoreTests
    {
        private const int DefaultTimeout = 5 * 60 * 1000; // 5 minutes
        private const string LoadMSBuildAssembliesMarkerFileName = "LoadMSBuildAssemblies.ran";

        /// <summary>
        /// Set by the MSTest framework. Used to write the MSBuild output into the test results
        /// and to attach the captured stdout/stderr as result files.
        /// </summary>
        public TestContext TestContext { get; set; } = null!;

        /// <summary>
        /// This test simulates a task running as part of the CollectPackageReferences target, which loads assemblies
        /// out of MSBuild's bin directory, so the assemblies are loaded before NuGet's Restore task starts. It is intended
        /// to validate whether MSBuild ships any assemblies that cause assembly loading conflicts with NuGet's Restore task.
        /// </summary>
        /// <remarks>
        /// Key assemblies tested:
        /// <list type="bullet">
        /// <item>Newtonsoft.Json: NuGet.Protocol's HTTP deserialization</item>
        /// <item>Microsoft.Extensions.FileSystemGlobbing: contentFiles with a wildcard include</item>
        /// </list>
        /// </remarks>
        [TestMethod]
        [Timeout(DefaultTimeout)]
        public async Task RestoreRunsWhenMSBuildDepdendenciesAreLoaded()
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
            string contentFileName = "sample.txt";
            string contentFilePackagePath = "contentFiles/any/any/" + contentFileName;

            // Ensure Microsoft.Extensions.FileSystemGlobbing.dll can be loaded by using
            // contentFiles with a wildcard include.
            var package = new SimpleTestPackageContext(packageName, packageVersion);
            package.Files.Clear();
            package.AddFile("lib/net472/_._");
            package.AddFile(contentFilePackagePath, "// content file served by the test package");
            package.Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>{packageName}</id>
    <version>{packageVersion}</version>
    <title>{packageName}</title>
    <authors>NuGet</authors>
    <description>{packageName}</description>
    <contentFiles>
      <files include=""any/any/*.txt"" buildAction=""Content"" copyToOutput=""true"" flatten=""false"" />
    </contentFiles>
  </metadata>
</package>");
            await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, package);

            // Test Newtonsoft.Json can be loaded by using a V3 HTTP feed, so json has to be deserialized.
            using var mockServer = new FileSystemBackedV3MockServer(pathContext.PackageSource);
            mockServer.Start();
            pathContext.Settings.RemoveSource(SimpleTestSettingsContext.DefaultPackageSourceName);
            pathContext.Settings.AddSource("mockSource", mockServer.ServiceIndexUri, allowInsecureConnectionsValue: "true");

            string projectName = "test";
            string projectPath = Path.Combine(pathContext.SolutionRoot, projectName + ".csproj");
            File.WriteAllText(projectPath, GetProjectXml(packageName, packageVersion));

            // Act
            var outputCapture = new CapturingTestOutputHelper();
            CommandRunnerResult result = CommandRunner.Run(
                filename: msbuildPath!,
                workingDirectory: pathContext.SolutionRoot,
                arguments: $"-t:restore \"{projectPath}\"",
                timeOutInMilliseconds: DefaultTimeout,
                testOutputHelper: outputCapture);

            CaptureMSBuildOutput(result, outputCapture);

            // Assert
            Assert.AreEqual(
                0,
                result.ExitCode,
                $"msbuild -t:restore failed (exit code {result.ExitCode}).{Environment.NewLine}{result.AllOutput}");

            string markerFilePath = Path.Combine(pathContext.SolutionRoot, "obj", LoadMSBuildAssembliesMarkerFileName);
            Assert.IsTrue(
                File.Exists(markerFilePath),
                $"Expected LoadMSBuildAssemblies to create '{markerFilePath}'.{Environment.NewLine}{result.AllOutput}");

            string markerFileContents = File.ReadAllText(markerFilePath);
            Assert.IsTrue(
                int.TryParse(markerFileContents, NumberStyles.Integer, CultureInfo.InvariantCulture, out int loadedAssemblyCount),
                $"Expected LoadMSBuildAssemblies marker file '{markerFilePath}' to contain an integer, but found '{markerFileContents}'." +
                Environment.NewLine +
                result.AllOutput);

            Assert.IsTrue(
                loadedAssemblyCount > 0,
                $"Expected LoadMSBuildAssemblies to load at least one assembly, but it loaded {loadedAssemblyCount}." +
                Environment.NewLine +
                result.AllOutput);

            string assetsFilePath = Path.Combine(pathContext.SolutionRoot, "obj", "project.assets.json");
            Assert.IsTrue(
                File.Exists(assetsFilePath),
                $"Expected restore to generate '{assetsFilePath}'.{Environment.NewLine}{result.AllOutput}");

            LockFile lockFile = new LockFileFormat().Read(assetsFilePath);

            // If restore successfully included the test package, this means it succesfully downloaded and parsed the flat
            // container's index.json version list, which validates that Newtonsoft.Json could be loaded.
            LockFileTargetLibrary? library = lockFile.Targets
                .SelectMany(target => target.Libraries)
                .FirstOrDefault(lib => StringComparer.OrdinalIgnoreCase.Equals(lib.Name, packageName));

            Assert.IsNotNull(
                library,
                $"Expected the assets file to contain a target library for '{packageName}'.{Environment.NewLine}{result.AllOutput}");

            // If the package's sample.txt is included as a content file with the correct action and copyToOutput value,
            // this means Microsoft.Extensions.FileSystemGlobbing could be loaded
            LockFileContentFile? selectedContentFile = library!.ContentFiles
                .FirstOrDefault(contentFile => StringComparer.OrdinalIgnoreCase.Equals(contentFile.Path, contentFilePackagePath));

            Assert.IsNotNull(
                selectedContentFile,
                $"Expected the assets file to select content file '{contentFilePackagePath}' for '{packageName}', " +
                $"but found: {string.Join(", ", library!.ContentFiles.Select(c => c.Path))}.{Environment.NewLine}{result.AllOutput}");

            Assert.AreEqual("Content", selectedContentFile.BuildAction.Value, ignoreCase: true);
            Assert.AreEqual(true, selectedContentFile.CopyToOutput);
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
            // Raw string literal with a doubled interpolation prefix ($$), so {{ }} delimit the
            // interpolation holes and the inline task's single braces are treated as literal text.
            return $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net472</TargetFramework>
                    <LoadMSBuildAssembliesMarkerFile>$(MSBuildProjectDirectory)\obj\{{LoadMSBuildAssembliesMarkerFileName}}</LoadMSBuildAssembliesMarkerFile>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="{{packageName}}" Version="{{packageVersion}}" />
                  </ItemGroup>

                  <!--
                    Simulate a task that loads all the assemblies in the MSBuild bin directory.
                  -->
                  <UsingTask TaskName="LoadMSBuildAssemblies" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
                    <ParameterGroup>
                      <Directory ParameterType="System.String" Required="true" />
                      <MarkerFile ParameterType="System.String" Required="true" />
                    </ParameterGroup>
                    <Task>
                      <Code Type="Fragment" Language="cs"><![CDATA[
                        int loadedAssemblyCount = 0;

                        foreach (string file in System.IO.Directory.GetFiles(Directory, "*.dll"))
                        {
                            try
                            {
                                System.Reflection.Assembly.LoadFrom(file);
                                loadedAssemblyCount++;
                            }
                            catch
                            {
                                // Skip assemblies that fail to load.
                            }
                        }

                        string markerDirectory = System.IO.Path.GetDirectoryName(MarkerFile);
                        if (!string.IsNullOrEmpty(markerDirectory))
                        {
                            System.IO.Directory.CreateDirectory(markerDirectory);
                        }

                        System.IO.File.WriteAllText(
                            MarkerFile,
                            loadedAssemblyCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                      ]]></Code>
                    </Task>
                  </UsingTask>

                  <Target Name="LoadMSBuildAssembliesBeforeRestore" BeforeTargets="CollectPackageReferences">
                    <LoadMSBuildAssemblies Directory="$(MSBuildBinPath)" MarkerFile="$(LoadMSBuildAssembliesMarkerFile)" />
                  </Target>
                </Project>
                """;
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
        /// CommandRunnerResult doesn't maintain the correct order of stderr and stdout messages, and this test project
        /// uses MSTest, whereas CommandRunner takes in xUnit's ITestOutputHelper. So, this helper is a simple buffer
        /// that captures both stdout and stderr in the order they were received.
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
