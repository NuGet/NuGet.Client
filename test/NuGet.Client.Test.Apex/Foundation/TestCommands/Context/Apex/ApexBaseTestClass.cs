using System;
using System.IO;
using FluentAssertions;
using Foundation.TestAttributes;
using Foundation.TestAttributes.Context;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Services;
using Microsoft.Test.Apex.VisualStudio;
using Foundation;

namespace NuGet.Client.Tests.Apex
{
    /// <summary>
    /// Base class for a Foundation-based test class supporting contexts and using the Apex framework.
    /// </summary>
    [ContextDefaultBehavior(ContextBehavior.RunAllContexts)]
    [RunOnTestUIThread]
    public abstract class ApexBaseTestClass : TestClass, IDisposable
    {
        private readonly Lazy<IVerifier> lazyVerifier;
        private readonly Lazy<ITestLogger> lazyLogger;
        private readonly Lazy<IExecutionScopeService> lazyScope;
        private IAssertionVerifier assertionVerifier;
        public abstract VisualStudioHost VisualStudio { get; }

        public ApexBaseTestClass()
        {
            this.lazyVerifier = new Lazy<IVerifier>(() => this.GetApexService<ITestLoggerFactoryService>().GetOrCreate("Testcase"));
            this.lazyLogger = new Lazy<ITestLogger>(() => this.GetApexService<ITestLogger>());
            this.lazyScope = new Lazy<IExecutionScopeService>(() => this.GetApexService<IExecutionScopeService>());
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
    }
}
