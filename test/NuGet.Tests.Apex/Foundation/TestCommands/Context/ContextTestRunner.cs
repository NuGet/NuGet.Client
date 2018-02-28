using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Tests.Foundation.TestAttributes;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGet.Tests.Foundation.TestCommands.Context
{
    public class ContextTestRunner : XunitTestRunner
    {
        private TestAttributes.Context.Context context;

        public ContextTestRunner(ITest test, 
                                    IMessageBus messageBus, 
                                    Type testClass, 
                                    object[] constructorArguments, 
                                    MethodInfo testMethod, 
                                    object[] testMethodArguments, 
                                    string skipReason, 
                                    IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, 
                                    ExceptionAggregator aggregator, 
                                    CancellationTokenSource cancellationTokenSource,
                                    TestAttributes.Context.Context context) 
            : base(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource)
        {
            this.context = context;
        }

        protected override async Task<decimal> InvokeTestMethodAsync(ExceptionAggregator aggregator)
        {
            if (TestMethodArguments != null)
            {
                for (int i = 0; i < TestMethodArguments.Length; i++)
                {
                    InlineDataAttribute.EnumShim enumShim;
                    InlineDataAttribute.TypeShim typeShim;

                    if ((enumShim = TestMethodArguments[i] as InlineDataAttribute.EnumShim) != null)
                    {
                        TestMethodArguments[i] = enumShim.GetUnderlyingEnum();
                    }
                    else if ((typeShim = TestMethodArguments[i] as InlineDataAttribute.TypeShim) != null)
                    {
                        TestMethodArguments[i] = typeShim.GetUnderlyingType();
                    }
                }
            }

            return await new ContextTestInvoker(Test, MessageBus, TestClass, ConstructorArguments, TestMethod, TestMethodArguments, BeforeAfterAttributes, aggregator, CancellationTokenSource, context).RunAsync();
        }
    }
}
