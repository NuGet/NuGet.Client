// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Tests.Apex
{
    [CollectionDefinition("SharedVSHost")]
    public sealed class SharedVisualStudioHostTestCollectionDefinition : ICollectionFixture<VisualStudioHostFixtureFactory>
    {
        private SharedVisualStudioHostTestCollectionDefinition()
        {
            throw new InvalidOperationException("SharedVisualStudioHostTestCollectionDefinition only exists for metadata, it should never be constructed.");
        }
    }

    [Collection("SharedVSHost")]
    public abstract class SharedVisualStudioHostTestClass : ApexBaseTestClass
    {
        private readonly IVisualStudioHostFixtureFactory _contextFixtureFactory;
        private readonly Lazy<VisualStudioHostFixture> _hostFixture;

        /// <summary>
        /// ITestOutputHelper wrapper
        /// </summary>
        public XunitLogger XunitLogger { get; }

        protected SharedVisualStudioHostTestClass(IVisualStudioHostFixtureFactory contextFixtureFactory, ITestOutputHelper output)
        {
            XunitLogger = new XunitLogger(output);
            _contextFixtureFactory = contextFixtureFactory;

            _hostFixture = new Lazy<VisualStudioHostFixture>(() =>
            {
                return _contextFixtureFactory.GetVisualStudioHostFixture();
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

        public override void CloseVisualStudioHost()
        {
            VisualStudio.Stop();
        }

        protected NuGetConsoleTestExtension GetConsole(ProjectTestExtension project)
        {
            XunitLogger.LogInformation("GetConsole");
            VisualStudio.ClearWindows();
            var nugetTestService = GetNuGetTestService();

            XunitLogger.LogInformation("EnsurePackageManagerConsoleIsOpen");
            nugetTestService.EnsurePackageManagerConsoleIsOpen().Should().BeTrue("Console was opened");

            XunitLogger.LogInformation("GetPackageManagerConsole");
            var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);

            nugetTestService.WaitForAutoRestore();

            XunitLogger.LogInformation("GetConsole complete");

            return nugetConsole;
        }

        public IOperations Operations => _hostFixture.Value.Operations;
    }
}
