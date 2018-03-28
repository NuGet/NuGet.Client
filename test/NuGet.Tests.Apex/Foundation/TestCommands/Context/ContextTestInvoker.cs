using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGet.Tests.Foundation.TestCommands.Context
{
    public class ContextTestInvoker : XunitTestInvoker
    {
        private TestAttributes.Context.Context context;
        private object testClassInstance;

        public ContextTestInvoker(ITest test, 
                                    IMessageBus messageBus, 
                                    Type testClass, 
                                    object[] constructorArguments, 
                                    MethodInfo testMethod, 
                                    object[] testMethodArguments, 
                                    IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, 
                                    ExceptionAggregator aggregator, 
                                    CancellationTokenSource cancellationTokenSource,
                                    TestAttributes.Context.Context context) 
            : base(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, beforeAfterAttributes, aggregator, cancellationTokenSource)
        {
            this.context = context;
        }

        public virtual void BeforeTestRun(object classInstance) { }
        public virtual void AfterTestRun(object classInstance) { }

        protected override Task BeforeTestMethodInvokedAsync()
        {
            this.BeforeTestRun(this.testClassInstance);

            return base.BeforeTestMethodInvokedAsync();
        }

        protected override Task AfterTestMethodInvokedAsync()
        {
            this.AfterTestRun(this.testClassInstance);

            return base.AfterTestMethodInvokedAsync();
        }

        protected override object CreateTestClass()
        {
            testClassInstance = base.CreateTestClass();
            TestClass testClass = testClassInstance as TestClass;
            if (testClass != null)
            {
                testClass.CurrentContext = this.context;
            }

            return this.testClassInstance;
        }
    }
}
