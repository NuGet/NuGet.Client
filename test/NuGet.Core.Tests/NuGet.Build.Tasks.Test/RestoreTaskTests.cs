// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Moq;
using NuGet.Common;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class RestoreTaskTests
    {
        [Theory]
        [InlineData("5", "7", 5, 7)]
        [InlineData("5", "0", 5, 0)]
        [InlineData(null, null, 0, 0)]
        [InlineData("invalid", "7", 0, 0)]
        [InlineData("0", "7", 0, 0)]
        [InlineData("-1", "7", 0, 0)]
        [InlineData("5", "-1", 0, 0)]
        [InlineData("5", "invalid", 0, 0)]
        [InlineData("5", "7", 0, 0, true)]
        public async Task Execute_PrunePackageReference_UsesDeclarationLocationWhenAvailable(string line, string column, int expectedLine, int expectedColumn, bool duplicate = false)
        {
            using var paths = new SimpleTestPathContext();
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(paths.PackageSource, new SimpleTestPackageContext("X", "1.0.0"));
            string projectPath = Path.Combine(paths.SolutionRoot, "project.csproj");
            string sourcePath = Path.Combine(paths.SolutionRoot, "references%20.props");
            var packageReference = new MockTaskItem("X")
            {
                ["Version"] = "1.0.0",
                ["DefiningProjectFullPath"] = sourcePath,
                ["DefiningProjectLineNumber"] = line,
                ["DefiningProjectColumnNumber"] = column,
            };

            BuildWarningEventArgs warning = null;
            string error = null;
            var engine = new Mock<IBuildEngine>();
            engine.SetupGet(e => e.ProjectFileOfTaskNode).Returns(projectPath);
            engine.Setup(e => e.LogWarningEvent(It.IsAny<BuildWarningEventArgs>())).Callback<BuildWarningEventArgs>(e => warning = e);
            engine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(e => error = e.Message);
            var references = new GetRestorePackageReferencesTask
            {
                ProjectUniqueName = projectPath,
                PackageReferences = duplicate ? [packageReference, packageReference] : [packageReference],
            };
            Assert.True(references.Execute());
            using var restore = new RestoreTask
            {
                BuildEngine = engine.Object,
                RestoreGraphItems = references.RestoreGraphItems.Concat<ITaskItem>(
                [
                    new MockTaskItem("project")
                    {
                        ["Type"] = "ProjectSpec", ["ProjectUniqueName"] = projectPath,
                        ["ProjectPath"] = projectPath, ["ProjectStyle"] = "PackageReference",
                        ["ProjectName"] = "project",
                        ["OutputPath"] = Path.Combine(paths.SolutionRoot, "obj"),
                        ["PackagesPath"] = paths.UserPackagesFolder, ["Sources"] = paths.PackageSource,
                        ["ConfigFilePaths"] = paths.NuGetConfig, ["NuGetAudit"] = "false",
                    },
                    new MockTaskItem("restore") { ["Type"] = "RestoreSpec", ["ProjectUniqueName"] = projectPath },
                    new MockTaskItem("framework")
                    {
                        ["Type"] = "TargetFrameworkInformation", ["ProjectUniqueName"] = projectPath,
                        ["TargetFramework"] = "net10.0", ["TargetFrameworkMoniker"] = ".NETCoreApp,Version=v10.0",
                        ["RestoreEnablePackagePruning"] = "true",
                    },
                    new MockTaskItem("prune")
                    {
                        ["Type"] = "PrunePackageReference", ["ProjectUniqueName"] = projectPath,
                        ["Id"] = "X", ["VersionRange"] = "1.0.0",
                    },
                ]).ToArray(),
            };

            Assert.True(restore.Execute(), error);
            Assert.NotNull(warning);
            Assert.Equal("NU1510", warning.Code);
            Assert.Equal(expectedLine > 0 ? sourcePath : projectPath, warning.File);
            Assert.Equal(expectedLine, warning.LineNumber);
            Assert.Equal(expectedColumn, warning.ColumnNumber);
            var persisted = new LockFileFormat().Read(Path.Combine(paths.SolutionRoot, "obj", "project.assets.json")).LogMessages.Single();
            Assert.Equal(expectedLine > 0 ? sourcePath : projectPath, persisted.FilePath);
            Assert.Equal(expectedLine, persisted.StartLineNumber);
            Assert.Equal(expectedColumn, persisted.StartColumnNumber);
        }

        // StaticState exposes process-global events, and there is no way to clear their subscriptions, so each test
        // subscribes a private marker handler, asserts only on its own local counter, and unsubscribes in a finally.

        [Fact]
        public void Execute_MultipleRestoresInOneBuild_DoesNotEndRestoreStateUntilTheBuildEnds()
        {
            var buildEngine = new TestBuildEngine();
            int ended = 0;
            Action handler = () => ended++;
            StaticState.BuildEnded += handler;

            try
            {
                // A build can restore more than once - the Arcade SDK restores its toolset and then the solution from
                // a single target - and the second restore is entitled to reuse the plugin processes the first one
                // started. Neither restore may end the shared restore state.
                Execute(buildEngine);
                Assert.Equal(0, ended);

                Execute(buildEngine);
                Assert.Equal(0, ended);

                // MSBuild disposes build-lifetime task objects when the build ends, including before it reuses the
                // node for the next build. That is when the state must be torn down, exactly once.
                buildEngine.DisposeRegisteredTaskObjects(RegisteredTaskObjectLifetime.Build);
                Assert.Equal(1, ended);
            }
            finally
            {
                StaticState.BuildEnded -= handler;
            }
        }

        [Fact]
        public void Execute_BuildEngineDoesNotSupportRegisteredTaskObjects_EndsRestoreStateImmediately()
        {
            var buildEngine = new LegacyTestBuildEngine();
            int ended = 0;
            Action handler = () => ended++;
            StaticState.BuildEnded += handler;

            try
            {
                // A host that cannot defer the teardown to the end of the build must still get one, so fall back to
                // ending the restore state here rather than leaking plugin processes into a reused process.
                Execute(buildEngine);

                Assert.Equal(1, ended);
            }
            finally
            {
                StaticState.BuildEnded -= handler;
            }
        }

        private static void Execute(IBuildEngine buildEngine)
        {
            using (var task = new RestoreTask
            {
                BuildEngine = buildEngine,
                RestoreGraphItems = Array.Empty<ITaskItem>(),
                HideWarningsAndErrors = true,
            })
            {
                Assert.True(task.Execute());
            }
        }

        /// <summary>
        /// A build engine that predates <see cref="IBuildEngine4" /> and so cannot hold an object for the lifetime of
        /// the build.
        /// </summary>
        private sealed class LegacyTestBuildEngine : IBuildEngine
        {
            public int ColumnNumberOfTaskNode => 0;

            public bool ContinueOnError => false;

            public int LineNumberOfTaskNode => 0;

            public string ProjectFileOfTaskNode => string.Empty;

            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => true;

            public void LogCustomEvent(CustomBuildEventArgs e) { }

            public void LogErrorEvent(BuildErrorEventArgs e) { }

            public void LogMessageEvent(BuildMessageEventArgs e) { }

            public void LogWarningEvent(BuildWarningEventArgs e) { }
        }
    }
}
