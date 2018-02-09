using System;
using System.IO;
using Apex.NuGetClient.TestServices;
using FluentAssertions;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Services;
using Microsoft.Test.Apex.VisualStudio;
using NuGetClient.Test.Integration.Fixtures;
using NuGetClient.Test.Foundation;
using NuGetClient.Test.Foundation.TestAttributes;
using NuGetClient.Test.Foundation.TestAttributes.Context;
using Xunit;

namespace NuGetClient.Test.Integration.Apex
{
    /// <summary>
    /// Base class for a Foundation-based test class supporting contexts and using the Apex framework.
    /// </summary>
    [ContextDefaultBehavior(ContextBehavior.RunAllContexts)]
    [RunOnTestUIThread]
    public abstract class ApexBaseTestClass : TestClass, IClassFixture<ApexTestRequirementsFixture>,IDisposable
    {
        private readonly Lazy<IVerifier> lazyVerifier;
        private readonly Lazy<ITestLogger> lazyLogger;
        private readonly Lazy<IExecutionScopeService> lazyScope;
        private IAssertionVerifier assertionVerifier;
        public abstract VisualStudioHost VisualStudio { get; }

        private readonly Lazy<NuGetApexTestService> nuGetPackageManagerTestService;

        public ApexBaseTestClass()
        {
            this.lazyVerifier = new Lazy<IVerifier>(() => this.GetApexService<ITestLoggerFactoryService>().GetOrCreate("Testcase"));
            this.lazyLogger = new Lazy<ITestLogger>(() => this.GetApexService<ITestLogger>());
            this.lazyScope = new Lazy<IExecutionScopeService>(() => this.GetApexService<IExecutionScopeService>());

            nuGetPackageManagerTestService = new Lazy<NuGetApexTestService>(() => this.VisualStudio.Get<NuGetApexTestService>());
        }

        public IVerifier Verify
        {
            get { return this.lazyVerifier.Value; }
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

        public virtual void Dispose()
        {
            if (this.AssertionVerify != null)
            {
                string omniLogFilePath = Path.Combine(Omni.Logging.Log.LogDirectory, "OmniLog.html");
                this.AssertionVerify.HasFailures.Should().BeFalse(because: string.Format("no assertion failure should be encountered during test (One or more assertion failures encountered, please find more details at file://{0})", omniLogFilePath));
            }
        }

        public virtual NuGetApexTestService GetNuGetTestService()
        {
            EnsureVisualStudioHostForContext();
            return nuGetPackageManagerTestService.Value;
        }
    }
}
