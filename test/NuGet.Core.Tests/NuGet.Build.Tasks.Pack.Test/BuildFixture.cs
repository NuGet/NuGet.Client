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

namespace NuGet.Build.Tasks.Pack.Test
{
    [CollectionDefinition(Name)]
    public class FixtureCollection
        : ICollectionFixture<BuildFixture>
    {
        internal const string Name = "Build Tests";
    }

    public class BuildFixture : IDisposable
    {
#if DEBUG
        const string CONFIGURATION = "Debug";
#else
        const string CONFIGURATION = "Release";
#endif

        const string FILENAME_DLL = "NuGet.Build.Tasks.Pack.dll";
        const string FILENAME_TARGETS = "NuGet.Build.Tasks.Pack.targets";

        internal readonly bool _isDotNetFramework = false;
        internal readonly string _testFrameworkMoniker = "netstandard2.0";

#if IS_DESKTOP
        private const string SdkVersion = "10";
        private const string SdkTfm = "net10.0";
#endif
        internal readonly string _pathDotnetExe;
        internal readonly string _pathMSBuildExe;
        internal readonly string _pathDllFile;
        internal readonly string _pathTargetsFile;

        internal readonly IReadOnlyDictionary<string, string> _dotnetEnvironments;

        public BuildFixture()
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
            var dllLocation = typeof(NuGet.Build.Tasks.Pack.GetPackOutputItemsTask).Assembly.Location;
            var dllDirectory = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks.Pack", "bin", CONFIGURATION, _testFrameworkMoniker);
            if (!System.IO.Directory.Exists(dllDirectory))
            {
                dllDirectory = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks.Pack", "bin", CONFIGURATION, _testFrameworkMoniker);
            }

            _pathDllFile = Path.Combine(dllDirectory, FILENAME_DLL);

            // https://github.com/NuGet/NuGet.Client/pull/6712
            // NuGet.Build.Tasks.Pack.targets has been moved to in NuGet.Build.Tasks project.
            // Therefore, NuGet.Build.Tasks project must be built before running this test.
            var tfmTargets = GetFrameworkMoniker(typeof(PackageFileNameTests), out var _);
            _pathTargetsFile = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks", "bin", CONFIGURATION, tfmTargets, FILENAME_TARGETS);
            if (!System.IO.File.Exists(_pathTargetsFile))
            {
                _pathTargetsFile = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks", "bin", CONFIGURATION, _testFrameworkMoniker, FILENAME_TARGETS);
                if (!System.IO.File.Exists(_pathTargetsFile))
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
                ["PATH"] = $"{_cliDirectory}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}"
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
            if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT)
            {
                var msbuildexe = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v4.0.30319", "msbuild.exe");

                var vswhereexe = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
                var runresult = CommandRunner.Run(
                        vswhereexe,
                        System.Environment.CurrentDirectory,
                        @" -latest -find MSBuild\**\Bin\MSBuild.exe");
                if (runresult.Success)
                {
                    msbuildexe = new System.IO.StringReader(runresult.Output).ReadLine() ?? "";
                }
                return msbuildexe;
            }
            else
            {
                return "";
            }
        }

        public void Dispose()
        {
        }

    }
}
