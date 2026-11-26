// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
#if !NETFRAMEWORK
using Microsoft.Build.Framework;
#endif
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class GenerateRestoreGraphFileTaskTests
    {
        [Fact]
        public void Cancel_WhenCanceled_CancellationTokenSourceIsCancellationRequestedIsTrue()
        {
            using (var task = new GenerateRestoreGraphFileTask())
            {
                task.Cancel();

                task._cancellationTokenSource.IsCancellationRequested.Should().BeTrue();
            }
        }

        [Fact]
        public void GetCommandLineArguments_WhenOptionsSpecified_CorrectValuesReturned()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                string msbuildBinPath = Path.Combine(testDirectory, "MSBuild", "Current", "Bin");
                string projectPath = Path.Combine(testDirectory, "src", "project1", "project1.csproj");
                string restoreGraphOutputPath = Path.Combine(testDirectory, "out.dgspec.json");
                var globalProperties = new Dictionary<string, string>
                {
                    ["Property1"] = "Value1",
                    ["Property2"] = "  Value2  "
                };

                var buildEngine = new TestBuildEngine(globalProperties);

                using (var task = new GenerateRestoreGraphFileTask
                {
                    BuildEngine = buildEngine,
                    MSBuildBinPath = msbuildBinPath,
                    ProjectFullPath = projectPath,
                    Recursive = true,
                    MSBuildStartupDirectory = testDirectory,
                    RestoreGraphOutputPath = restoreGraphOutputPath,
                })
                {
                    string arguments = task.GetCommandLineArguments(globalProperties);

                    arguments.Should().Be($"\"{string.Join("\" \"", GetExpectedArguments(msbuildBinPath, projectPath))}\"");
                }

                IEnumerable<string> GetExpectedArguments(string msbuildBinPath, string projectPath)
                {
#if IS_CORECLR
                    yield return Path.ChangeExtension(typeof(RestoreTaskEx).Assembly.Location, ".Console.dll");
#endif
                    yield return $"Recursive=True;GenerateRestoreGraphFile=True;RestoreGraphOutputPath={restoreGraphOutputPath}";
#if IS_CORECLR
                    yield return Path.Combine(msbuildBinPath, "MSBuild.dll");
#else
                    yield return Path.Combine(msbuildBinPath, "MSBuild.exe");
#endif
                    yield return projectPath;

                    yield return $"Property1=Value1;Property2=  Value2  ";
                }
            }
        }

        [Fact]
        public void GetProcessFileName_WhenCalled_ReturnsCorrectValue()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                string msbuildBinPath = Path.Combine(testDirectory, "MSBuild", "Current", "Bin");

                using (var task = new GenerateRestoreGraphFileTask
                {
                    MSBuildBinPath = msbuildBinPath
                })
                {
#if IS_CORECLR
                    task.GetProcessFileName(null).Should().Be(Path.Combine(testDirectory, "MSBuild", "dotnet"));
#else
                    task.GetProcessFileName(null).Should().Be(Path.ChangeExtension(typeof(RestoreTaskEx).Assembly.Location, ".Console.exe"));
#endif
                }
            }
        }

        [Fact]
        public void GetProcessFileName_WithExePathParameter_ReturnsCorrectValue()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                string msbuildBinPath = Path.Combine(testDirectory, "MSBuild", "Current", "Bin");
                string exePath = Path.Combine(testDirectory, "override.exe");

                using (var task = new GenerateRestoreGraphFileTask
                {
                    MSBuildBinPath = msbuildBinPath
                })
                {
                    task.GetProcessFileName(exePath).Should().Be(exePath);
                }
            }
        }

        [Theory]
        [InlineData("false", true, "EnableMultiThreaded=False", true)]
        [InlineData("invalid;Recursive=False", false, null, false)]
        [InlineData(null, false, null, false)]
        public void GetCommandLineArguments_WhenMultiThreadedOrDisableParallelSpecified_EmitsExpectedOptions(
            string enableMultiThreaded,
            bool disableParallel,
            string expectedMultiThreadedOption,
            bool expectDisableParallel)
        {
            using var testDirectory = TestDirectory.Create();
            string msbuildBinPath = Path.Combine(testDirectory, "MSBuild", "Current", "Bin");
            string projectPath = Path.Combine(testDirectory, "src", "project1", "project1.csproj");
            var globalProperties = new Dictionary<string, string>();

            using var task = new GenerateRestoreGraphFileTask
            {
                BuildEngine = new TestBuildEngine(globalProperties),
                DisableParallel = disableParallel,
                EnableMultiThreaded = enableMultiThreaded,
                MSBuildBinPath = msbuildBinPath,
                ProjectFullPath = projectPath,
                MSBuildStartupDirectory = testDirectory,
                RestoreGraphOutputPath = Path.Combine(testDirectory, "out.dgspec.json"),
            };

            string arguments = task.GetCommandLineArguments(globalProperties);

            if (expectedMultiThreadedOption != null)
            {
                arguments.Should().Contain(expectedMultiThreadedOption);
            }
            else
            {
                arguments.Should().NotContain("EnableMultiThreaded=");
            }

            if (expectDisableParallel)
            {
                arguments.Should().Contain("DisableParallel=True");
            }
            else
            {
                arguments.Should().NotContain("DisableParallel=");
            }
        }

#if !NETFRAMEWORK
        [Fact]
        public void GetProcessFileName_WhenRelativeOverrideSpecified_ResolvesAgainstTaskEnvironmentProjectDirectory()
        {
            using var testDirectory = TestDirectory.Create();
            string projectDir = Path.Combine(testDirectory, "proj");
            Directory.CreateDirectory(projectDir);

            using var task = new GenerateRestoreGraphFileTask
            {
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                MSBuildBinPath = Path.Combine(testDirectory, "MSBuild", "Current", "Bin"),
            };

            task.GetProcessFileName(Path.Combine("tools", "custom.exe"))
                .Should().Be(Path.Combine(projectDir, "tools", "custom.exe"));
        }
#endif
    }
}
