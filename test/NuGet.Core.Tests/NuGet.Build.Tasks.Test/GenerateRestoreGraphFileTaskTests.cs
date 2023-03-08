// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
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
                    var arguments = task.GetCommandLineArguments().ToList();
                    arguments.Should().BeEquivalentTo(
#if IS_CORECLR
                    Path.ChangeExtension(typeof(RestoreTaskEx).Assembly.Location, ".Console.dll"),
#endif
                    $"GenerateRestoreGraphFile=True;Recursive=True;RestoreGraphOutputPath={restoreGraphOutputPath}",
#if IS_CORECLR
                    Path.Combine(msbuildBinPath, "MSBuild.dll"),
#else
                    Path.Combine(msbuildBinPath, "MSBuild.exe"),
#endif
                    projectPath,
                        $"Property1=Value1;Property2=  Value2  ;ExcludeRestorePackageImports=true;OriginalMSBuildStartupDirectory={testDirectory}");
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
    }
}
