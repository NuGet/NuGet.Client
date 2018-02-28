using System.Threading;
using System.Threading.Tasks;
using NuGet.Tests.Foundation.TestAttributes.Context;
using Xunit.Sdk;

namespace NuGet.Tests.Foundation.TestCommands.Context
{
    public class ContextTestCaseRunner : XunitTestCaseRunner
    {
        private TestAttributes.Context.Context context;
        public ContextTestCaseRunner(IXunitTestCase testCase,
                                        string displayName,
                                        string skipReason,
                                        object[] constructorArguments,
                                        object[] testMethodArguments,
                                        IMessageBus messageBus,
                                        ExceptionAggregator aggregator,
                                        CancellationTokenSource cancellationTokenSource,
                                        TestAttributes.Context.Context context)
            : base(testCase, displayName, skipReason, constructorArguments, testMethodArguments, messageBus, aggregator, cancellationTokenSource)
        {
            this.context = context;
        }

        protected override async Task<RunSummary> RunTestAsync()
        {
            var test = new XunitTest(TestCase, DisplayName);
            return await new ContextTestRunner(test, MessageBus, TestClass, ConstructorArguments, TestMethod, TestMethodArguments, SkipReason, BeforeAfterAttributes, new ExceptionAggregator(Aggregator), CancellationTokenSource, this.context).RunAsync();
        }
    }
}
