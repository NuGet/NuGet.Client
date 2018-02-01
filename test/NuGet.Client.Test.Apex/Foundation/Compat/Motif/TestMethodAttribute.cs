using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using NuGetClient.Test.Foundation.TestAttributes;
using NuGetClient.Test.Foundation.TestAttributes.Context;
using NuGetClient.Test.Foundation.TestCommands.Context;
using NuGetClient.Test.Foundation.Utility;
using Xunit.Abstractions;
using Xunit.Sdk;
using MotifRuntime = NuGetClient.Test.Foundation.Compat.Motif;

namespace NuGetClient.Test.Foundation.Compat.Motif
{
    // Name has to match MS.Internal.Test.Automation.Office.Runtime.TestMethodAttribute to allow compiling against this assembly instead of MOTIF

    /// <summary>
    /// Shim to allow running tests built against MOTIF. Intent is to convert users and depreciate.
    /// </summary>
    [XunitTestCaseDiscoverer("Foundation.Compat.Motif", "Foundation")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TestMethodAttribute : TestAttribute
    {
        public TestMethodAttribute() : base() { }

        public TestMethodAttribute(string setupName, string teardownName)
            : base()
        {
            this.SetupMethodName = setupName;
            this.TeardownMethodName = teardownName;
        }

        public string SetupMethodName { get; private set; }
        public string TeardownMethodName { get; private set; }
    }

    public class MotifTestCaseDiscoverer : ContextTestDiscoverer
    {
        public MotifTestCaseDiscoverer(IMessageSink diagnosticMessageSink) : base(diagnosticMessageSink)
        {
        }

        public override ContextBaseTestCase GetContextTest(Context context, ITestMethod testMethod)
        {
            return new MotifTestCase(diagnosticMessageSink, TestMethodDisplay.Method, testMethod, context);
        }
    }

    public class MotifTestCase : ContextTestCase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer", true)]
        public MotifTestCase() : base()
        {
        }

        public MotifTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay testMethodDisplay, ITestMethod testMethod, Context context, object[] testMethodArguments = null)
            : base(diagnosticMessageSink, testMethodDisplay, testMethod, context, testMethodArguments)
        {
        }

        protected override async Task<RunSummary> RunTest(IMessageSink diagnosticMessageSink,
                                                    IMessageBus messageBus,
                                                    object[] constructorArguments,
                                                    ExceptionAggregator aggregator,
                                                    CancellationTokenSource cancellationTokenSource)
        {
            return await new MotifTestCaseRunner(this, DisplayName, SkipReason, constructorArguments, TestMethodArguments, messageBus, aggregator, cancellationTokenSource, this.Context).RunAsync();
        }
    }

    public class MotifTestCaseRunner : ContextTestCaseRunner
    {
        private Context context;
        public MotifTestCaseRunner(IXunitTestCase testCase,
                                        string displayName,
                                        string skipReason,
                                        object[] constructorArguments,
                                        object[] testMethodArguments,
                                        IMessageBus messageBus,
                                        ExceptionAggregator aggregator,
                                        CancellationTokenSource cancellationTokenSource,
                                        Context context)
            : base(testCase, displayName, skipReason, constructorArguments, testMethodArguments, messageBus, aggregator, cancellationTokenSource, context)
        {
            this.context = context;
        }

        protected override Task<RunSummary> RunTestAsync()
        {
            var test = new XunitTest(TestCase, DisplayName);
            return new MotifTestRunner(test, MessageBus, TestClass, ConstructorArguments, TestMethod, TestMethodArguments, SkipReason, BeforeAfterAttributes, new ExceptionAggregator(Aggregator), CancellationTokenSource, this.context).RunAsync();
        }
    }

    public class MotifTestRunner : ContextTestRunner
    {
        private Context context;
        public MotifTestRunner(ITest test,
                                    IMessageBus messageBus,
                                    Type testClass,
                                    object[] constructorArguments,
                                    MethodInfo testMethod,
                                    object[] testMethodArguments,
                                    string skipReason,
                                    IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
                                    ExceptionAggregator aggregator,
                                    CancellationTokenSource cancellationTokenSource,
                                    Context context)
            : base(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource, context)
        {
            this.context = context;
        }

        protected override async Task<decimal> InvokeTestMethodAsync(ExceptionAggregator aggregator)
        {
            return await new MotifTestInvoker(Test, MessageBus, TestClass, ConstructorArguments, TestMethod, TestMethodArguments, BeforeAfterAttributes, aggregator, CancellationTokenSource, context).RunAsync();
        }
    }

    public class MotifTestInvoker : ContextTestInvoker
    {
        ExceptionDispatchInfo coreException;

        public MotifTestInvoker(ITest test,
                                    IMessageBus messageBus,
                                    Type testClass,
                                    object[] constructorArguments,
                                    MethodInfo testMethod,
                                    object[] testMethodArguments,
                                    IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
                                    ExceptionAggregator aggregator,
                                    CancellationTokenSource cancellationTokenSource,
                                    Context context)
            : base(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, beforeAfterAttributes, aggregator, cancellationTokenSource, context)
        {
        }

        public override void BeforeTestRun(object classInstance)
        {
            base.BeforeTestRun(classInstance);

            // Find the attributed [Setup] methods for the class
            Type testClassType = TestMethod.DeclaringType;
            MethodInfo testClassSetupMethod = null;

            foreach (MethodInfo method in testClassType.GetMethods())
            {
                if (method.GetCustomAttribute<MotifRuntime.SetupAttribute>() != null) { testClassSetupMethod = method; }
            }

            Attribute info = TestMethod.GetCustomAttributes(typeof(MotifRuntime.TestMethodAttribute)).FirstOrDefault();
            if ((info as MotifRuntime.TestMethodAttribute) == null)
            {
                return;
            }

            MotifRuntime.TestMethodAttribute testMethodAttribute = (MotifRuntime.TestMethodAttribute)info;

            ExceptionDispatchInfo exception = null;

            // Run the setup methods
            exception = this.InvokeMethod(classInstance, testClassSetupMethod);

            if (exception != null)
            {
                exception.Throw();
            }

            exception = this.InvokeMethod(testClassType, classInstance, testMethodAttribute.SetupMethodName, "test setup method");

            if (exception != null)
            {
                exception.Throw();
            }
        }

        protected override async Task<decimal> InvokeTestMethodAsync(object testClassInstance)
        {
            try
            {
                using (new AssertExceptionHandler())
                {
                    // Invoke just returns Timer.Total
                    await base.InvokeTestMethodAsync(testClassInstance);
                }
            }
            catch (Exception e)
            {
                coreException = ExceptionDispatchInfo.Capture(e);
            }

            return Timer.Total;
        }

        public override void AfterTestRun(object classInstance)
        {
            base.AfterTestRun(classInstance);

            // Find the attributed [Setup] methods for the class
            Type testClassType = TestMethod.DeclaringType;
            MethodInfo testClassTeardownMethod = null;

            foreach (MethodInfo method in testClassType.GetMethods())
            {
                if (method.GetCustomAttribute<MotifRuntime.TeardownAttribute>() != null) { testClassTeardownMethod = method; }
            }

            Attribute info = TestMethod.GetCustomAttributes(typeof(MotifRuntime.TestMethodAttribute)).FirstOrDefault();
            if ((info as MotifRuntime.TestMethodAttribute) == null)
            {
                return;
            }

            MotifRuntime.TestMethodAttribute testMethodAttribute = (MotifRuntime.TestMethodAttribute)info;

            ExceptionDispatchInfo exception = null;

            // Run the teardown methods (to attempt cleanup for next tests), only throw if we don't have a core exception
            exception = this.InvokeMethod(testClassType, classInstance, testMethodAttribute.TeardownMethodName, "test teardown method");
            if (exception != null && coreException == null)
            {
                exception.Throw();
            }

            exception = this.InvokeMethod(classInstance, testClassTeardownMethod);
            if (exception != null && coreException == null)
            {
                exception.Throw();
            }

            if (coreException != null)
            {
                coreException.Throw();
            }
        }

        private ExceptionDispatchInfo InvokeMethod(object testClass, MethodInfo method)
        {
            if (method == null)
            {
                return null;
            }

            try
            {
                method.Invoke(testClass, null);
            }
            catch (TargetInvocationException e)
            {
                // Need to rethrow so Assertions can bubble
                return ExceptionDispatchInfo.Capture(e.InnerException);
            }

            return null;
        }

        private ExceptionDispatchInfo InvokeMethod(Type testClassType, object testClass, string methodName, string type)
        {
            if (!string.IsNullOrEmpty(methodName))
            {
                // Find and run the teardown method
                MethodInfo method = testClassType.GetMethod(methodName);
                if (method == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not find {1} method '{0}'", methodName, type));
                }
                else
                {
                    return this.InvokeMethod(testClass, method);
                }
            }

            return null;
        }
    }
}
