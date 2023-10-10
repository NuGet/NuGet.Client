// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGet.Test.Utility
{
    public class RetryFactDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly IMessageSink _diagnosticMessageSink;

        public RetryFactDiscoverer(IMessageSink diagnosticMessageSink)
        {
            _diagnosticMessageSink = diagnosticMessageSink;
        }

        IEnumerable<IXunitTestCase> IXunitTestCaseDiscoverer.Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            int maxRetries = factAttribute.GetNamedArgument<int>("MaxRetries");

            yield return new RetryTestCase(_diagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod, maxRetries);
        }

        private class DelayedMessageBus : ConcurrentQueue<IMessageSinkMessage>, IMessageBus
        {
            public void Dispose()
            {
            }

            public bool QueueMessage(IMessageSinkMessage message)
            {
                Enqueue(message);

                return true;
            }

            public void SendMessages(IMessageBus messageBus)
            {
                foreach (IMessageSinkMessage message in this)
                {
                    messageBus.QueueMessage(message);
                }
            }
        }

        [Serializable]
        private class RetryTestCase : XunitTestCase
        {
            private int _maxRetries;

            [EditorBrowsable(EditorBrowsableState.Never)]
            [Obsolete()]
            public RetryTestCase()
            {
            }

            public RetryTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay testMethodDisplay, TestMethodDisplayOptions defaultMethodDisplayOptions, ITestMethod testMethod, int maxRetries)
                : base(diagnosticMessageSink, testMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments: null)
            {
                _maxRetries = maxRetries;
            }

            public override void Deserialize(IXunitSerializationInfo data)
            {
                base.Deserialize(data);

                _maxRetries = data.GetValue<int>("MaxRetries");
            }

            public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            {
                var runCount = 0;

                while (true)
                {
                    DelayedMessageBus delayedMessageBus = new();

                    RunSummary summary = await base.RunAsync(diagnosticMessageSink, delayedMessageBus, constructorArguments, aggregator, cancellationTokenSource);
                    if (aggregator.HasExceptions || summary.Failed == 0 || ++runCount >= _maxRetries)
                    {
                        delayedMessageBus.SendMessages(messageBus);

                        return summary;
                    }

                    diagnosticMessageSink.OnMessage(new DiagnosticMessage("Execution of '{0}' failed (attempt #{1}), retrying...", DisplayName, runCount));
                }
            }

            public override void Serialize(IXunitSerializationInfo data)
            {
                base.Serialize(data);

                data.AddValue("MaxRetries", _maxRetries);
            }
        }
    }
}
