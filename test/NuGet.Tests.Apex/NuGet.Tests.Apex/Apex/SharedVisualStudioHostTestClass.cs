// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NuGet.Tests.Apex
{
    [TestClass]
    public abstract class SharedVisualStudioHostTestClass : ApexBaseTestClass
    {
        private static readonly ConcurrentDictionary<string, IVisualStudioHostFixtureFactory> _contextFixtureFactories = new();
        private readonly Lazy<VisualStudioHostFixture> _hostFixture;
        private NuGetConsoleTestExtension _console;
        private string _packageManagerOutputWindowText;

        public const int DefaultTimeout = 5 * 60 * 1000; // 5 minutes

        protected SharedVisualStudioHostTestClass()
        {
            _hostFixture = new Lazy<VisualStudioHostFixture>(() =>
            {
                IVisualStudioHostFixtureFactory contextFixtureFactory = _contextFixtureFactories.GetOrAdd(
                    TestContext.FullyQualifiedTestClassName,
                    _ => new VisualStudioHostFixtureFactory());

                return contextFixtureFactory.GetVisualStudioHostFixture();
            });
        }

        public override VisualStudioHost VisualStudio => _hostFixture.Value.VisualStudio;

        public override TService GetApexService<TService>()
        {
            return _hostFixture.Value.Operations.Get<TService>();
        }

        public override void EnsureVisualStudioHost()
        {
            _hostFixture.Value.EnsureHost();
        }

        public override void CleanupVisualStudioHost()
        {
            if (!_hostFixture.IsValueCreated)
            {
                return;
            }

            VisualStudioHostFixture hostFixture = _hostFixture.Value;

            if (!hostFixture.IsHostRunning)
            {
                hostFixture.Dispose();
                return;
            }

            if (HasVerificationFailures || TestContext.CurrentTestOutcome != UnitTestOutcome.Passed)
            {
                Logger.WriteMessage(
                    $"Test '{TestContext.TestName}' did not pass. Visual Studio will be restarted before the next test.");
                hostFixture.Dispose();
                return;
            }

            try
            {
                hostFixture.VisualStudio.RuntimeReset();
            }
            catch (Exception ex)
            {
                Logger.WriteWarning(
                    $"Visual Studio failed to reset after test '{TestContext.TestName}'. The next test will use a new instance. {ex}");
                hostFixture.Dispose();
                return;
            }

            Logger.WriteMessage(
                $"Test '{TestContext.TestName}' passed. The next test in this class will reuse Visual Studio process '{hostFixture.VisualStudio.HostProcess.Id}'.");
        }

        protected NuGetConsoleTestExtension GetConsole(ProjectTestExtension project)
        {
            Logger.WriteMessage("GetConsole");
            VisualStudio.ClearWindows();
            NuGetApexTestService nugetTestService = GetNuGetTestService();

            Logger.WriteMessage("EnsurePackageManagerConsoleIsOpen");
            nugetTestService.EnsurePackageManagerConsoleIsOpen().Should().BeTrue("Console was opened");

            Logger.WriteMessage("GetPackageManagerConsole");
            _console = nugetTestService.GetPackageManagerConsole(project.Name);

            // This is not a magic number.
            // It is intended to eliminate unexpected hard line breaks in PMC output which might foil validation,
            // but not so large as to create memory problems.
            _console.SetConsoleWidth(consoleWidth: 1024);

            nugetTestService.WaitForAutoRestore();

            Logger.WriteMessage("GetConsole complete");


            return _console;
        }

        public override void Dispose()
        {
            if (_hostFixture.IsValueCreated && _hostFixture.Value.IsHostRunning)
            {
                LogVisualStudioOutput();
            }

            base.Dispose();
        }

        private void LogVisualStudioOutput()
        {
            try
            {
                if (_console != null)
                {
                    string text = _console.GetText();

                    Logger.WriteMessage($"Package Manager Console contents:  {text}");
                }

                _packageManagerOutputWindowText ??= GetPackageManagerOutputWindowPaneText();

                Logger.WriteMessage($"Package Manager Output Window Pane contents:  {_packageManagerOutputWindowText}");
            }
            catch (Exception ex)
            {
                Logger.WriteWarning($"Failed to collect Visual Studio output during test cleanup. {ex}");
            }
        }

        internal string GetPackageManagerOutputWindowPaneText()
        {
            return string.Join(Environment.NewLine, VisualStudio.GetOutputWindowsLines());
        }

        [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
        public static void ClassCleanup()
        {
            foreach (string testClassName in _contextFixtureFactories.Keys)
            {
                if (_contextFixtureFactories.TryRemove(
                    testClassName,
                    out IVisualStudioHostFixtureFactory contextFixtureFactory))
                {
                    contextFixtureFactory.Dispose();
                }
            }
        }

        [TestInitialize]
        public override void TestInitialize()
        {
            base.TestInitialize();

            EnsureVisualStudioHost();
        }
    }
}
