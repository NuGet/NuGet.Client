using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using NuGetClient.Test.Foundation.Utility;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGetClient.Test.Foundation.TestCommands
{
    public class UIThreadTestCase : XunitTestCase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer", true)]
        public UIThreadTestCase() { }

        public UIThreadTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay testMethodDisplay, ITestMethod testMethod, object[] testMethodArguments = null)
            : base(diagnosticMessageSink, testMethodDisplay, testMethod, testMethodArguments)
        {
        }

        protected override string GetDisplayName(IAttributeInfo factAttribute, string displayName)
        {
            var proposedDisplayName = base.GetDisplayName(factAttribute, displayName);
            // Strip any characters that are not legal XML, as test results typically get serialized
            // to XML later and downstream sources may not properly handle invalid XML characters.
            // https://en.wikipedia.org/wiki/Valid_characters_in_XML
            string re = @"[^\x09\x0A\x0D\x20-\uD7FF\uE000-\uFFFD\u10000-\u10FFFF]";
            return Regex.Replace(proposedDisplayName, re, "?");
        }

        protected virtual async Task<RunSummary> RunTest(IMessageSink diagnosticMessageSink,
                                                IMessageBus messageBus,
                                                object[] constructorArguments,
                                                ExceptionAggregator aggregator,
                                                CancellationTokenSource cancellationTokenSource)
        {
            return await base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
        }

        public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            // Poke to setup the application
            var appInstance = TestUIThreadHelper.Instance;

            if (TestUIThreadHelper.ShouldRunOnTestUIThread(this.TestMethod.TestClass.Class))
            {
                this.TraceTest(" on Test UI Thread dispatcher.\n");

                return await TestUIThreadHelper.Instance.InvokeOnTestUIThread<RunSummary>(async () =>
                {
                    using (new AssertExceptionHandler())
                    {
                        return await this.RunTest(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
                    }
                });
            }
            else if (TestUIThreadHelper.ShouldRunOnSTAThread(this.TestMethod.TestClass.Class))
            {
                this.TraceTest(" on STA thread.\n");
                using (new AssertExceptionHandler())
                {
                    return await this.RunSTA(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
                }
            }
            else
            {
                this.TraceTest(" on normal XUnit thread.\n");
                using (new AssertExceptionHandler())
                {
                    return await this.RunTest(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
                }
            }
        }

        private void TraceTest(string where)
        {
            if (Debugger.IsLogging())
            {
                Debugger.Log(0, null, "TEST: Executing test " + this.DisplayName + where);
            }
        }

        public Task<RunSummary> RunSTA(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            this.TraceTest(" on xUnit STA thread.\n");
            using (new AssertExceptionHandler())
            {
                var tcs = new TaskCompletionSource<RunSummary>();
                var thread = new Thread(() =>
                {
                    try
                    {
                        // Start off the test method.
                        var testCaseTask = this.RunTest(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);

                        // Arrange to pump messages to execute any async work associated with the test.
                        var frame = new DispatcherFrame();
                        Task.Run(async delegate
                        {
                            try
                            {
                                await testCaseTask;
                            }
                            finally
                            {
                                // The test case's execution is done. Terminate the message pump.
                                frame.Continue = false;
                            }
                        });
                        Dispatcher.PushFrame(frame);

                        // Report the result back to the Task we returned earlier.
                        TestUIThreadHelper.CopyTaskResultFrom(tcs, testCaseTask);
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                });

                thread.Name = "xUnit STA Thread";
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                return tcs.Task;
            }
        }
    }
}
