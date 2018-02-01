using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGetClient.Test.Foundation.TestCommands.Context
{
    /// <summary>
    /// We will fall back to this when the PropertyData members cannot be pre-enumerated.  This happens when they are not basic system types or do not
    /// derive from IXunitSerializable
    /// </summary>
    public class ContextTheoryTestCaseRunner : XunitTestCaseRunner
    {
        static readonly object[] NoArguments = new object[0];

        readonly ExceptionAggregator cleanupAggregator = new ExceptionAggregator();
        Exception dataDiscoveryException;
        readonly IMessageSink diagnosticMessageSink;
        readonly List<ContextTheoryTestRunner> testRunners = new List<ContextTheoryTestRunner>();
        readonly List<IDisposable> toDispose = new List<IDisposable>();
        TestAttributes.Context.Context context;

        public ContextTheoryTestCaseRunner(IXunitTestCase testCase,
                                         string displayName,
                                         string skipReason,
                                         object[] constructorArguments,
                                         IMessageSink diagnosticMessageSink,
                                         IMessageBus messageBus,
                                         ExceptionAggregator aggregator,
                                         CancellationTokenSource cancellationTokenSource,
                                         TestAttributes.Context.Context context)
            : base(testCase, displayName, skipReason, constructorArguments, NoArguments, messageBus, aggregator, cancellationTokenSource)
        {
            this.diagnosticMessageSink = diagnosticMessageSink;
            this.context = context;
        }

        protected override async Task AfterTestCaseStartingAsync()
        {
            await base.AfterTestCaseStartingAsync();

            try
            {
                var dataAttributes = TestCase.TestMethod.Method.GetCustomAttributes(typeof(DataAttribute));

                foreach (var dataAttribute in dataAttributes)
                {
                    IAttributeInfo discovererAttribute = dataAttribute.GetCustomAttributes(typeof(DataDiscovererAttribute)).First();
                    List<string> args = discovererAttribute.GetConstructorArguments().Cast<string>().ToList();
                    Type discovererType = typeof(MemberDataDiscoverer);
                    IDataDiscoverer discoverer = ExtensibilityPointFactory.GetDataDiscoverer(diagnosticMessageSink, discovererType);

                    foreach (var dataRow in discoverer.GetData(dataAttribute, TestCase.TestMethod.Method))
                    {
                        toDispose.AddRange(dataRow.OfType<IDisposable>());

                        ITypeInfo[] resolvedTypes = null;
                        var methodToRun = TestMethod;

                        if (methodToRun.IsGenericMethodDefinition)
                        {
                            resolvedTypes = TypeUtility.ResolveGenericTypes(TestCase.TestMethod.Method, dataRow);
                            methodToRun = methodToRun.MakeGenericMethod(resolvedTypes.Select(t => ((IReflectionTypeInfo)t).Type).ToArray());
                        }

                        Type[] parameterTypes = methodToRun.GetParameters().Select(p => p.ParameterType).ToArray();
                        object[] convertedDataRow = Reflector.ConvertArguments(dataRow, parameterTypes);
                        string theoryDisplayName = TypeUtility.GetDisplayNameWithArguments(TestCase.TestMethod.Method, DisplayName, convertedDataRow, resolvedTypes);
                        XunitTest test = new XunitTest(TestCase, theoryDisplayName);

                        testRunners.Add(new ContextTheoryTestRunner(test, MessageBus, TestClass, ConstructorArguments, methodToRun, convertedDataRow, SkipReason, BeforeAfterAttributes, Aggregator, CancellationTokenSource, context));
                    }
                }
            }
            catch (Exception ex)
            {
                // Stash the exception so we can surface it during RunTestAsync
                dataDiscoveryException = ex;
            }
        }

        protected override Task BeforeTestCaseFinishedAsync()
        {
            Aggregator.Aggregate(cleanupAggregator);

            return base.BeforeTestCaseFinishedAsync();
        }

        protected override async Task<RunSummary> RunTestAsync()
        {
            if (dataDiscoveryException != null)
            {
                return RunTest_DataDiscoveryException();
            }

            RunSummary runSummary = new RunSummary();
            foreach (ContextTheoryTestRunner testRunner in testRunners)
            {
                runSummary.Aggregate(await testRunner.RunAsync());
            }

            // Run the cleanup here so we can include cleanup time in the run summary,
            // but save any exceptions so we can surface them during the cleanup phase,
            // so they get properly reported as test case cleanup failures.
            ExecutionTimer timer = new ExecutionTimer();
            foreach (var disposable in toDispose)
            {
                timer.Aggregate(() => cleanupAggregator.Run(() => disposable.Dispose()));
            }

            runSummary.Time += timer.Total;
            return runSummary;
        }

        RunSummary RunTest_DataDiscoveryException()
        {
            XunitTest test = new XunitTest(TestCase, DisplayName);

            if (!MessageBus.QueueMessage(new TestStarting(test)))
            {
                CancellationTokenSource.Cancel();
            }
            else if (!MessageBus.QueueMessage(new TestFailed(test, 0, null, Unwrap(dataDiscoveryException))))
            {
                CancellationTokenSource.Cancel();
            }
            if (!MessageBus.QueueMessage(new TestFinished(test, 0, null)))
            {
                CancellationTokenSource.Cancel();
            }

            return new RunSummary { Total = 1, Failed = 1 };
        }

        private static Exception Unwrap(Exception ex)
        {
            while (true)
            {
                TargetInvocationException tiex = ex as TargetInvocationException;
                if (tiex == null)
                {
                    return ex;
                }

                ex = tiex.InnerException;
            }
        }
    }
}
