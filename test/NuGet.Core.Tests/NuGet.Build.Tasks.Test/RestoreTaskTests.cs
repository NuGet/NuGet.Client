// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Moq;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace NuGet.Build.Tasks.Test
{
    [CollectionDefinition(nameof(RestoreTaskCollection), DisableParallelization = true)]
    public sealed class RestoreTaskCollection
    {
    }

    [Collection(nameof(RestoreTaskCollection))]
    public class RestoreTaskTests
    {
        [Theory]
        [InlineData(true, "success")]
        [InlineData(false, "success")]
        [InlineData(true, "error")]
        [InlineData(false, "error")]
        [InlineData(true, "canceled")]
        [InlineData(false, "canceled")]
        public async Task Execute_WithNonPumpingSynchronizationContext_Completes(bool disableParallel, string outcome)
        {
            using var directory = TestDirectory.Create();
            using var releaseRestore = new ManualResetEventSlim(initialState: false);
            string projectPath = Path.Combine(directory, "project.csproj");
            string outputPath = Path.Combine(directory, "obj");
            string configPath = Path.Combine(directory, "NuGet.Config");
            File.WriteAllText(configPath, "<configuration><packageSources><clear /></packageSources></configuration>");
            var engine = new TestBuildEngine();
            var gatedEngine = new Mock<IBuildEngine4>();
            int callingThread = 0;
            gatedEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(e =>
                {
                    if (Thread.CurrentThread.ManagedThreadId != callingThread)
                    {
                        Assert.True(releaseRestore.Wait(TimeSpan.FromSeconds(30)), "The caller did not enter its synchronous wait.");
                    }
                    engine.LogMessageEvent(e);
                });
            gatedEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(engine.LogErrorEvent);
            gatedEngine.Setup(e => e.LogWarningEvent(It.IsAny<BuildWarningEventArgs>()))
                .Callback<BuildWarningEventArgs>(engine.LogWarningEvent);
            gatedEngine.Setup(e => e.RegisterTaskObject(
                It.IsAny<object>(), It.IsAny<object>(), It.IsAny<RegisteredTaskObjectLifetime>(), It.IsAny<bool>()))
                .Callback<object, object, RegisteredTaskObjectLifetime, bool>(engine.RegisterTaskObject);

            using var task = new RestoreTask
            {
                BuildEngine = gatedEngine.Object,
                RestoreDisableParallel = disableParallel,
                RestoreGraphItems = new[]
                {
                    CreateItem("RestoreSpec"),
                    CreateItem("ProjectSpec",
                        ("ProjectName", "project"), ("ProjectPath", projectPath),
                        ("ProjectStyle", "PackageReference"), ("OutputPath", outputPath),
                        ("PackagesPath", Path.Combine(directory, "packages")), ("ConfigFilePaths", configPath),
                        ("NuGetAudit", "false")),
                    CreateItem("TargetFrameworkInformation",
                        ("TargetFramework", "net472"), ("TargetFrameworkIdentifier", ".NETFramework"),
                        ("TargetFrameworkVersion", "v4.7.2"), ("TargetFrameworkMoniker", ".NETFramework,Version=v4.7.2"))
                }
            };

            if (outcome == "error")
            {
                var items = new List<ITaskItem>(task.RestoreGraphItems)
                {
                    CreateItem("Dependency", ("Id", "Missing.Package"), ("VersionRange", "1.0.0"), ("TargetFrameworks", "net472"))
                };
                task.RestoreGraphItems = items.ToArray();
            }

            try
            {
                await NonPumpingSynchronizationContext.RunAsync(() =>
                {
                    callingThread = Thread.CurrentThread.ManagedThreadId;
                    bool succeeded = task.Execute();
                    Assert.True(succeeded == (outcome == "success"), engine.TestLogger.ShowMessages());
                }, onWait: () =>
                {
                    if (outcome == "canceled")
                    {
                        task.Cancel();
                    }
                    releaseRestore.Set();
                });

                if (outcome == "canceled")
                {
                    Assert.Contains(engine.TestLogger.LogMessages,
                        message => message.Level == LogLevel.Error && message.Message == Strings.RestoreCanceled);
                }
                else
                {
                    Assert.Equal(1, task.ProjectsRestored);
                    Assert.Equal(0, task.ProjectsAlreadyUpToDate);
                    Assert.True(File.Exists(Path.Combine(outputPath, "project.assets.json")));
                    Assert.Equal(outcome == "error", engine.TestLogger.Errors > 0);
                }
            }
            finally
            {
                engine.DisposeRegisteredTaskObjects(RegisteredTaskObjectLifetime.Build);
            }

            ITaskItem CreateItem(string type, params (string Name, string Value)[] metadata)
            {
                var item = new TaskItem(Guid.NewGuid().ToString());
                item.SetMetadata("Type", type);
                item.SetMetadata("ProjectUniqueName", projectPath);
                foreach ((string name, string value) in metadata)
                {
                    item.SetMetadata(name, value);
                }
                return item;
            }
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
