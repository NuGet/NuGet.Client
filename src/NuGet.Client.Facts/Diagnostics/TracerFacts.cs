// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.Client.Diagnostics
{
    public class TracerFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void GivenANullInvocationId_ItThrowsArgNull()
            {
                Assert.Throws<ArgumentNullException>("invocationId", () => new TraceContext(null, TraceSinks.Null));
            }

            [Fact]
            public void GivenANullTraceSink_ItThrowsArgNull()
            {
                Assert.Throws<ArgumentNullException>("sink", () => new TraceContext("foo", null));
            }
        }

        public class TheTraceMethods
        {
            [Fact]
            public void EnterPassesInvocationIdThrough()
            {
                PassThroughTest(
                    t => t.Enter("method", "file", 42),
                    (id, m) => m.Verify(s => s.Enter(id, "method", "file", 42)));
            }

            [Fact]
            public void SendRequestPassesInvocationIdThrough()
            {
                var req = new HttpRequestMessage();
                PassThroughTest(
                    t => t.SendRequest(req, "method", "file", 42),
                    (id, m) => m.Verify(s => s.SendRequest(id, req, "method", "file", 42)));
            }

            [Fact]
            public void ReceiveResponsePassesInvocationIdThrough()
            {
                var resp = new HttpResponseMessage();
                PassThroughTest(
                    t => t.ReceiveResponse(resp, "method", "file", 42),
                    (id, m) => m.Verify(s => s.ReceiveResponse(id, resp, "method", "file", 42)));
            }

            [Fact]
            public void ErrorPassesInvocationIdThrough()
            {
                var ex = new Exception();
                PassThroughTest(
                    t => t.Error(ex, "method", "file", 42),
                    (id, m) => m.Verify(s => s.Error(id, ex, "method", "file", 42)));
            }

            [Fact]
            public void ExitPassesInvocationIdThrough()
            {
                PassThroughTest(
                    t => t.Exit("method", "file", 42),
                    (id, m) => m.Verify(s => s.Exit(id, "method", "file", 42)));
            }

            [Fact]
            public void JsonParseIssuePassesInvocationIdThrough()
            {
                var jobj = new JObject();
                PassThroughTest(
                    t => t.JsonParseWarning(jobj, "warning", "method", "file", 42),
                    (id, m) => m.Verify(s => s.JsonParseWarning(id, jobj, "warning", "method", "file", 42)));
            }

            private void PassThroughTest(Action<TraceContext> tracerAction, Action<string, Mock<ITraceSink>> sinkVerifier)
            {
                // Arrange
                var sink = new Mock<ITraceSink>();
                var tracer = new TraceContext("foo", sink.Object);

                // Act
                tracerAction(tracer);

                // Assert
                sinkVerifier("foo", sink);
            }
        }
    }
}
