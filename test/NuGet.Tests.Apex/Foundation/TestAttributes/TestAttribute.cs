using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Tests.Foundation.TestAttributes.Context;
using NuGet.Tests.Foundation.TestCommands.Context;
using NuGet.Tests.Foundation.Utility;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGet.Tests.Foundation.TestAttributes
{
    /// <summary>
    /// Used to mark a test method
    /// </summary>
    /// <remarks>
    /// Simple wrapper for XUnit FactAttribute to isolate tests. In root namespace for discoverability.
    /// </remarks>    
    [XunitTestCaseDiscoverer("NuGet.Tests.Foundation.TestAttributes.ContextTestDiscoverer", "NuGet.Tests.Foundation")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TestAttribute : FactAttribute
    {
    }

    public abstract class SuiteContextTestDiscoverer : IXunitTestCaseDiscoverer
    {
        protected readonly IMessageSink diagnosticMessageSink;

        static SuiteContextTestDiscoverer()
        {
            string[] searchPaths = new string[]
                {
                    Environment.ExpandEnvironmentVariables("%_NTBINROOT%\\SuiteBin"),
                    Path.GetDirectoryName(Environment.ExpandEnvironmentVariables("%NuGetClientTestBinariesPath%")),
                    Path.GetDirectoryName(new Uri(typeof(TestAttribute).Assembly.CodeBase).AbsolutePath)
                };

            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs args)
            {
                return AssemblyResolver.ResolveAssembly(searchPaths, args.Name);
            };
        }

        public SuiteContextTestDiscoverer(IMessageSink diagnosticMessageSink)
        {
            this.diagnosticMessageSink = diagnosticMessageSink;
        }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            if (Environment.GetEnvironmentVariable("XUNIT_TEST_DISCOVERY") != null)
            {
                System.Diagnostics.Debugger.Launch();
            }

            TestSuite suite = TestSuite.Current;
            if (suite != null)
            {
                if (!suite.Tests.Any(suiteTest => testMethod.Method.Name == suiteTest.Name))
                {
                    return Enumerable.Empty<ContextBaseTestCase>();
                }
            }

            try
            {
                IEnumerable<Context.Context> contexts = ContextHelpers.Instance.GenerateTestCommands(testMethod);
                return this.GetTests(discoveryOptions, contexts, testMethod, factAttribute);
            }
            catch (Exception e)
            {
                return new IXunitTestCase[] { new ExecutionErrorTestCase(diagnosticMessageSink, Xunit.Sdk.TestMethodDisplay.Method, testMethod, $"Test discovery failed with exception: {e.ToString()}") };
            }
        }

        public virtual IEnumerable<IXunitTestCase> GetTests(ITestFrameworkDiscoveryOptions discoveryOptions, IEnumerable<Context.Context> contexts, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            return Enumerable.Empty<ContextBaseTestCase>();
        }
    }

    public class ContextTestDiscoverer : SuiteContextTestDiscoverer
    {
        public ContextTestDiscoverer(IMessageSink diagnosticMessageSink) : base(diagnosticMessageSink)
        {
        }

        public override IEnumerable<IXunitTestCase> GetTests(ITestFrameworkDiscoveryOptions discoveryOptions, IEnumerable<Context.Context> contexts, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            List<ContextBaseTestCase> contextTests = new List<ContextBaseTestCase>();

            if (contexts.Any())
            {
                foreach (Context.Context c in contexts)
                {
                    // Remove these checks once legacy platforms are removed from PlatformIdentifier
                    if (c.Platform == PlatformIdentifier.Wpf ||
                        c.Platform == PlatformIdentifier.UWP ||
                        c.Platform == PlatformIdentifier.UnspecifiedPlatform ||
                        c.Platform == PlatformIdentifier.Default)
                    {
                        contextTests.Add(this.GetContextTest(c, testMethod));
                    }
                }
            }
            else
            {
                contextTests.Add(this.GetContextTest(Context.Context.EmptyContext, testMethod));
            }
            return contextTests;
        }

        public virtual ContextBaseTestCase GetContextTest(Context.Context context, ITestMethod testMethod)
        {
            return new ContextTestCase(diagnosticMessageSink, TestMethodDisplay.Method, testMethod, context);
        }
    }
}
