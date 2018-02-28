using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Tests.Foundation.TestAttributes.Context;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGet.Tests.Foundation.TestCommands.Context
{
    public class ContextTheoryTestCase : ContextBaseTestCase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer", true)]
        public ContextTheoryTestCase() : base()
        {
        }

        public ContextTheoryTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay testMethodDisplay, ITestMethod testMethod, TestAttributes.Context.Context context, object[] testMethodArguments = null)
            : base(diagnosticMessageSink, testMethodDisplay, testMethod, context, testMethodArguments)
        {
        }

        protected override async Task<RunSummary> RunTest(IMessageSink diagnosticMessageSink,
                                                    IMessageBus messageBus,
                                                    object[] constructorArguments,
                                                    ExceptionAggregator aggregator,
                                                    CancellationTokenSource cancellationTokenSource)
        {
            return await new ContextTheoryTestCaseRunner(this, DisplayName, SkipReason, constructorArguments, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource, this.Context).RunAsync();
        }
    }
}
