// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess;
using NuGet.Frameworks;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

#if TEST_FOR_MSBUILD
namespace Msbuild.Integration.Test
#elif TEST_FOR_DOTNET
namespace Dotnet.Integration.Test
#endif
{
    /// <summary></summary>
    /// <remarks>The _GetOutputItemsFromPack target inside the NuGet.Build.Tasks.Pack.targets file is the subject of the test, so the project that includes NuGet.Build.Tasks.Pack.targets must be registered as a build dependency.</remarks>
    [Collection(PackageFileNameBuildTestFixtureCollection.Name)]
    public class PackageFileNameTests
    {
        private readonly PackageFileNameBuildTestFixture _testFixture;
        private readonly ITestOutputHelper _testOutputHelper;

        public PackageFileNameTests(PackageFileNameBuildTestFixture testFixture, ITestOutputHelper testOutputHelper)
        {
            _testFixture = testFixture;
            _testOutputHelper = testOutputHelper;
        }

        public static System.Collections.Generic.IEnumerable<object[]> PackageFileNameTestCases => PackageFileNameTestCase.TestCases;

        // This unit test verifies that the output file names from GetPackOutputItemsTask matches the output file names from PackTask.
        // Since PackTask does not expose the output file name as a property,
        // the current implementation performs an actual build and inspects the generated file.
        [PlatformTheory(Platform.Windows)]
        [MemberData(nameof(PackageFileNameTestCases))]
        public void PackTask_PackageFileName(PackageFileNameTestCase testCase)
        {
            string[] outputExtensions = PackageFileNameTestsCommon.GetOutputExtensions(testCase.IncludeSymbols, testCase.SymbolPackageFormat);

            using (var testDirectory = TestDirectory.Create())
            {
                PackageFileNameTestsCommon.CreateTestProjectFileAndNuspecFile(testCase, testDirectory, _testFixture._pathDllFile, _testFixture._pathTargetsFile, _testFixture._testFrameworkMoniker);

                CommandRunnerResult runresult;
                if (_testFixture._isDotNetFramework)
                { // This test is running on .Net Framework

                    // As noted in #6703, since the .NetStandard2.0 library has been removed,
                    // running tests on .net Framework requires invoking msbuild.exe.

                    // Restore (needs create project.assets.json)
                    _testFixture.Restore(testDirectory, PackageFileNameTestsCommon.FILENAME_PROJECT_FILE, _testOutputHelper);

                    // msbuild.exe
                    runresult = CommandRunner.Run(
                                       _testFixture._pathMSBuildExe,
                                       testDirectory,
                                       $"/t:Build;Pack /p:Configuration={PackageFileNameBuildTestFixture.CONFIGURATION} /p:UsingMicrosoftNetSdk=true {PackageFileNameTestsCommon.FILENAME_PROJECT_FILE}",
                                       environmentVariables: _testFixture._dotnetEnvironments,
                                       testOutputHelper: _testOutputHelper);
                }
                else
                {
                    // dotnet.exe
                    runresult = CommandRunner.Run(
                                        _testFixture._pathDotnetExe,
                                        testDirectory,
                                        $"build -p:Configuration={PackageFileNameBuildTestFixture.CONFIGURATION} {PackageFileNameTestsCommon.FILENAME_PROJECT_FILE}",
                                        environmentVariables: _testFixture._dotnetEnvironments,
                                        testOutputHelper: _testOutputHelper);
                }
                Assert.True(0 == runresult.ExitCode, runresult.Output + " " + runresult.Errors);

                var objFolder = System.IO.Path.Combine(testDirectory, "obj");
                var log = System.IO.File.ReadAllLines(System.IO.Path.Combine(objFolder, PackageFileNameTestsCommon.FILENAME_GETOUTPUTITEMSTASK_OUTPUTPACKITEMS_TEST));
                var lines = log.Where(line => !line.StartsWith(objFolder)).ToArray();

                var nupkgGeneratedFiles = outputExtensions
                        .SelectMany(outputExtension => Directory.GetFiles(testDirectory, $"*{outputExtension}", SearchOption.AllDirectories))
                        .Where(line => !line.StartsWith(objFolder))
                        .Distinct().ToArray();
                Assert.Equal(outputExtensions.Length, nupkgGeneratedFiles.Length);

                foreach (string outputNupkgName in testCase.OutputNupkgNames)
                {
                    var matchCountInFileSystem = PackageFileNameTestsCommon.GetNameMatchFilePathCount(outputNupkgName, nupkgGeneratedFiles);
                    Assert.True(matchCountInFileSystem == 1, $"{outputNupkgName} is not found in filesystem. [{string.Join(" , ", nupkgGeneratedFiles.Select(_ => System.IO.Path.GetFileName(_)))}]");

                    var matchCountInOutputPackItems = PackageFileNameTestsCommon.GetNameMatchFilePathCount(outputNupkgName, lines);
                    Assert.True(matchCountInOutputPackItems == 1, $"{outputNupkgName} is not found in OutputPackItems. [{string.Join(" , ", lines.Select(_ => System.IO.Path.GetFileName(_)))}]");
                }
            }
        }
    }

    [CollectionDefinition(Name)]
    public class PackageFileNameBuildTestFixtureCollection
        : ICollectionFixture<PackageFileNameBuildTestFixture>
    {
        internal const string Name = nameof(PackageFileNameBuildTestFixtureCollection) + "Collection";
    }

    public class PackageFileNameBuildTestFixture : IDisposable
    {
#if DEBUG
        public const string CONFIGURATION = "Debug";
#else
    public const string CONFIGURATION = "Release";
#endif

        private const string FILENAME_DLL = "NuGet.Build.Tasks.Pack.dll";
        private const string FILENAME_TARGETS = "NuGet.Build.Tasks.Pack.targets";

#if IS_DESKTOP
        private const string SdkVersion = "10";
        private const string SdkTfm = "net10.0";
#endif

        internal readonly bool _isDotNetFramework = false;
        internal readonly string _testFrameworkMoniker = "netstandard2.0";

        internal readonly string _pathDotnetExe;
        internal readonly string _pathMSBuildExe;
        internal readonly string _pathDllFile;
        internal readonly string _pathTargetsFile;

        internal readonly IReadOnlyDictionary<string, string> _dotnetEnvironments = new Dictionary<string, string>();

        public PackageFileNameBuildTestFixture()
        {
            _pathDotnetExe = NuGet.Test.Utility.TestFileSystemUtility.GetDotnetCli();

#if IS_DESKTOP
            var _cliDirectory = TestDotnetCLiUtility.CopyAndPatchLatestDotnetCli(SdkVersion, SdkTfm);
#else
            string testAssemblyPath = Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var _cliDirectory = TestDotnetCLiUtility.CopyAndPatchLatestDotnetCli(testAssemblyPath);
#endif
            var dotnetExecutableName = NuGet.Common.RuntimeEnvironmentHelper.IsWindows ? "dotnet.exe" : "dotnet";
            _pathDotnetExe = Path.Combine(_cliDirectory, dotnetExecutableName);

            var sdkPath = Directory.EnumerateDirectories(Path.Combine(_cliDirectory, "sdk"))
                .Single(d => !string.Equals(Path.GetFileName(d), "NuGetFallbackFolder", StringComparison.OrdinalIgnoreCase));

            _pathMSBuildExe = GetMsBuildExePath();
            _testFrameworkMoniker = GetFrameworkMoniker(typeof(NuGet.Build.Tasks.Pack.GetPackOutputItemsTask), out var isDotNetFramework);
            _isDotNetFramework = isDotNetFramework;

            var artifactsDirectory = NuGet.Test.Utility.TestFileSystemUtility.GetArtifactsDirectoryInRepo();
            var dllDirectory = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks.Pack", "bin", CONFIGURATION, _testFrameworkMoniker);
            _pathDllFile = Path.Combine(dllDirectory, FILENAME_DLL);

            // https://github.com/NuGet/NuGet.Client/pull/6712
            // NuGet.Build.Tasks.Pack.targets has been moved to in NuGet.Build.Tasks project.
            // Therefore, NuGet.Build.Tasks project must be built before running this test.
            var tfmTargets = GetFrameworkMoniker(typeof(PackageFileNameTests), out var _);
            _pathTargetsFile = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks", "bin", CONFIGURATION, tfmTargets, FILENAME_TARGETS);
            if (!File.Exists(_pathTargetsFile))
            {
                _pathTargetsFile = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks", "bin", CONFIGURATION, _testFrameworkMoniker, FILENAME_TARGETS);
                if (!File.Exists(_pathTargetsFile))
                {
                    _pathTargetsFile = Path.Combine(dllDirectory, FILENAME_TARGETS);
                }
            }

            _dotnetEnvironments = new Dictionary<string, string>()
            {
                ["MSBuildSDKsPath"] = Path.Combine(sdkPath, "Sdks"),
                ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
                ["DOTNET_ROOT"] = _cliDirectory,
                ["MSBuildExtensionsPath"] = new DirectoryInfo(sdkPath).FullName,
                ["PATH"] = $"{_cliDirectory}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}",

                //["DEBUG_RESTORE_TASK"] = $"{true}",
                //["DEBUG_PACK_TASK"] = $"{true}"
            };

            Assert.True(System.IO.File.Exists(_pathDllFile), $"{FILENAME_DLL} missing");
            Assert.True(System.IO.File.Exists(_pathTargetsFile), $"{FILENAME_TARGETS} missing");
        }

        private static string GetFrameworkMoniker(Type typeInAssembly, out bool isDotNetFramework)
        {
            var assembly = typeInAssembly.Assembly;
            var targetFrameworkAttribute
                = assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
                .OfType<System.Runtime.Versioning.TargetFrameworkAttribute>().FirstOrDefault();

            Assert.True(targetFrameworkAttribute != null, "Can't get targetFramework version");

            isDotNetFramework = targetFrameworkAttribute.FrameworkName.Contains(".NETFramework");
            return NuGetFramework.Parse(targetFrameworkAttribute.FrameworkName).GetShortFolderName();
        }

        private static string GetMsBuildExePath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var msbuildexe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v4.0.30319", "msbuild.exe");
                var vswhereexe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
                Assert.True(File.Exists(vswhereexe), "vswhere not found");

                var runresult = CommandRunner.Run(
                        vswhereexe,
                        Environment.CurrentDirectory,
                        @" -latest -find MSBuild\**\Bin\MSBuild.exe");
                if (runresult.Success)
                {
                    msbuildexe = new StringReader(runresult.Output).ReadLine() ?? "";
                }

                Assert.True(File.Exists(msbuildexe), "msbuild not found");
                return msbuildexe;
            }
            else
            {
                return "";
            }
        }

        //Create project.assets.json
        public void Restore(string testDirectory, string pathProjectFile, ITestOutputHelper _testOutputHelper)
        {
            CommandRunnerResult runresult;
            if (_isDotNetFramework)
            {
                runresult = CommandRunner.Run(
                    _pathMSBuildExe,
                    testDirectory,
                    $"/t:Restore /p:UsingMicrosoftNetSdk=true \"{pathProjectFile}\"",
                    environmentVariables: _dotnetEnvironments,
                    testOutputHelper: _testOutputHelper);
            }
            else
            {
                runresult = CommandRunner.Run(
                    _pathDotnetExe,
                    testDirectory,
                    $"restore \"{pathProjectFile}\"",
                    environmentVariables: _dotnetEnvironments,
                    testOutputHelper: _testOutputHelper);
            }
            Assert.True(runresult.ExitCode == 0, runresult.Output + " " + runresult.Errors);
        }
        public void Dispose()
        {
        }

    }

}
