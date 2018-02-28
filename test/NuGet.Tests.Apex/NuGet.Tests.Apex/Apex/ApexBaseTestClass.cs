// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Services;
using Microsoft.Test.Apex.VisualStudio;
using NuGet.Tests.Foundation;
using NuGet.Tests.Foundation.TestAttributes;
using NuGet.Tests.Foundation.TestAttributes.Context;
using Xunit;

namespace NuGet.Tests.Apex
{
    /// <summary>
    /// Base class for a Foundation-based test class supporting contexts and using the Apex framework.
    /// </summary>
    [ContextDefaultBehavior(ContextBehavior.RunAllContexts)]
    [RunOnTestUIThread]
    public abstract class ApexBaseTestClass : TestClass, IClassFixture<ApexTestRequirementsFixture>, IDisposable
    {
        private readonly Lazy<IVerifier> _lazyVerifier;
        private readonly Lazy<NuGetApexTestService> _nuGetPackageManagerTestService;

        // ITestLoggerSink Interface. Implemented by logger sites.
        private readonly Lazy<ITestLogger> lazyLogger;

        // Defines a mechanism for emitting execution scopes. 
        private readonly Lazy<IExecutionScopeService> lazyScope;

        // Interface for verification logging sinks.
        private IAssertionVerifier assertionVerifier;

        public ApexBaseTestClass()
        {
            _lazyVerifier = new Lazy<IVerifier>(() => GetApexService<ITestLoggerFactoryService>().GetOrCreate("Testcase"));
            this.lazyLogger = new Lazy<ITestLogger>(() => this.GetApexService<ITestLogger>());
            this.lazyScope = new Lazy<IExecutionScopeService>(() => this.GetApexService<IExecutionScopeService>());
            _nuGetPackageManagerTestService = new Lazy<NuGetApexTestService>(() => VisualStudio.Get<NuGetApexTestService>());
        }

        public abstract VisualStudioHost VisualStudio { get; }

        public IVerifier Verify
        {
            get { return _lazyVerifier.Value; }
        }

        public ITestLogger Log
        {
            get { return this.lazyLogger.Value; }
        }

        public IExecutionScopeService Scope
        {
            get { return this.lazyScope.Value; }
        }

        protected IAssertionVerifier AssertionVerify
        {
            get
            {
                if (this.assertionVerifier == null)
                {
                    this.assertionVerifier = this.GetApexService<IAssertionVerifier>();
                }
                return assertionVerifier;
            }
        }

        // TODO: This should probably be implicit in the constructor. Tests that
        //       want to control this explicitly (i.e. shutdown/restart VS in the test)
        //       should be able to, but default should probably be to start VS automatically.
        public abstract void EnsureVisualStudioHostForContext();

        public abstract void SetHostEnvironment(string name, string value);

        public abstract string GetHostEnvironment(string name);

        public abstract TService GetApexService<TService>() where TService : class;

        public abstract void EnsureVisualStudioHost();

        public abstract void CloseVisualStudioHost();

        public virtual NuGetApexTestService GetNuGetTestService()
        {
            EnsureVisualStudioHost();
            return _nuGetPackageManagerTestService.Value;
        }

        public virtual void Dispose()
        {
            if (this.AssertionVerify != null)
            {
                string omniLogFilePath = Path.Combine(Omni.Logging.Log.LogDirectory, "OmniLog.html");
                this.AssertionVerify.HasFailures.Should().BeFalse(because: string.Format("no assertion failure should be encountered during test (One or more assertion failures encountered, please find more details at file://{0})", omniLogFilePath));
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUGET_TEST_CLOSE_VS_AFTER_EACH_TEST")))
            {
                //test cleanup
                CloseVisualStudioHost();
            }
        }
    }
}
