using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NuGetClient.Test.Foundation.TestAttributes.Context;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGetClient.Test.Foundation.TestCommands.Context
{
    public class ContextTestCase : ContextBaseTestCase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer", true)]
        public ContextTestCase() : base()
        {
        }

        public ContextTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay testMethodDisplay, ITestMethod testMethod, TestAttributes.Context.Context context, object[] testMethodArguments = null)
            : base(diagnosticMessageSink, testMethodDisplay, testMethod, context, testMethodArguments)
        {
        }

        protected override async Task<RunSummary> RunTest(IMessageSink diagnosticMessageSink,
                                                    IMessageBus messageBus,
                                                    object[] constructorArguments,
                                                    ExceptionAggregator aggregator,
                                                    CancellationTokenSource cancellationTokenSource)
        {
            return await new ContextTestCaseRunner(this, DisplayName, SkipReason, constructorArguments, TestMethodArguments, messageBus, aggregator, cancellationTokenSource, this.Context).RunAsync();
        }
    }
}
