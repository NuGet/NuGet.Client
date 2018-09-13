// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Test.Server;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class HttpRetryHandlerTests
    {
        private const int MaxTries = 5;
        private const string TestUrl = "https://test.local/test.json";
        private static readonly TimeSpan SmallTimeout = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan LargeTimeout = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task HttpRetryHandler_HandlesFailureToConnect()
        {
            // Arrange
            var server = new NotListeningServer { Mode = TestServerMode.ConnectFailure };

            // Act & Assert
            var exception = await ThrowsException<HttpRequestException>(server);
#if IS_CORECLR
            Assert.NotNull(exception.InnerException);
            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                Assert.Equal("Couldn't connect to server", exception.InnerException.Message);
            }
            else
            {
                Assert.Equal("A connection with the server could not be established", exception.InnerException.Message);
            }
#else
            var innerException = Assert.IsType<WebException>(exception.InnerException);
            Assert.Equal(WebExceptionStatus.ConnectFailure, innerException.Status);
#endif
        }

        [Fact]
        public async Task HttpRetryHandler_HandlesInvalidProtocol()
        {
            // Arrange
            var server = new TcpListenerServer { Mode = TestServerMode.ServerProtocolViolation };

            // Act & Assert
            var exception = await ThrowsException<HttpRequestException>(server);
#if IS_CORECLR
            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                Assert.Null(exception.InnerException);
                Assert.Equal("The server returned an invalid or unrecognized response.", exception.Message);
            }
            else
            {
                Assert.NotNull(exception.InnerException);
                Assert.Equal("The server returned an invalid or unrecognized response", exception.InnerException.Message);
            }
#else
            var innerException = Assert.IsType<WebException>(exception.InnerException);
            Assert.Equal(WebExceptionStatus.ServerProtocolViolation, innerException.Status);
#endif
        }

        [Fact]
        public async Task HttpRetryHandler_HandlesNameResolutionFailure()
        {
            // Arrange
            var server = new UnknownDnsServer { Mode = TestServerMode.NameResolutionFailure };

            // Act & Assert
            var exception = await ThrowsException<HttpRequestException>(server);
#if IS_CORECLR
            Assert.NotNull(exception.InnerException);
            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                Assert.Equal("Couldn't resolve host name", exception.InnerException.Message);
            }
            else
            {
                Assert.Equal("The server name or address could not be resolved", exception.InnerException.Message);
            }
#else
            var innerException = Assert.IsType<WebException>(exception.InnerException);
            Assert.Equal(WebExceptionStatus.NameResolutionFailure, innerException.Status);
#endif
        }

        [Fact]
        public async Task HttpRetryHandler_DifferentRequestInstanceEachTime()
        {
            // Arrange
            var requests = new HashSet<HttpRequestMessage>();
            Func<HttpRequestMessage, HttpResponseMessage> handler = requestMessage =>
            {
                requests.Add(requestMessage);
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            };

            var retryHandler = new HttpRetryHandler();
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, TestUrl))
            {
                MaxTries = MaxTries,
                RequestTimeout = Timeout.InfiniteTimeSpan,
                RetryDelay = TimeSpan.Zero
            };
            var log = new TestLogger();

            // Act
            using (await retryHandler.SendAsync(request, log, CancellationToken.None))
            {
            }

            // Assert
            Assert.Equal(MaxTries, requests.Count);
        }

        [Fact]
        public async Task HttpRetryHandler_AppliesTimeoutToRequestsIndividually()
        {
            // Arrange

            // 20 requests that take 250ms each for a total of 5 seconds (plus noise).
            var requestDuration = TimeSpan.FromMilliseconds(250);
            var maxTries = 20;

            // Make the request timeout longer than each request duration but less than the total
            // duration of all attempts.
            var requestTimeout = TimeSpan.FromMilliseconds(4000);

            int hits = 0;
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler = async (requestMessage, token) =>
            {
                hits++;
                await Task.Delay(requestDuration);
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            };

            var retryHandler = new HttpRetryHandler();
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, TestUrl))
            {
                MaxTries = maxTries,
                RequestTimeout = requestTimeout,
                RetryDelay = TimeSpan.Zero
            };
            var log = new TestLogger();

            // Act
            using (await retryHandler.SendAsync(request, log, CancellationToken.None))
            {
            }

            // Assert
            Assert.Equal(maxTries, hits);
        }

        [Fact]
        public async Task HttpRetryHandler_CancelsRequestAfterTimeout()
        {
            // Arrange
            CancellationToken requestToken = CancellationToken.None;
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler = async (requestMessage, token) =>
            {
                requestToken = token;
                await Task.Delay(LargeTimeout, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryHandler = new HttpRetryHandler();
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, TestUrl))
            {
                MaxTries = 1,
                RequestTimeout = SmallTimeout,
                RetryDelay = TimeSpan.Zero
            };

            // Act
            Func<Task> actionAsync = () => retryHandler.SendAsync(
                request,
                new TestLogger(),
                CancellationToken.None);

            // Assert
            await Assert.ThrowsAsync<TimeoutException>(actionAsync);
            Assert.True(requestToken.CanBeCanceled);
            Assert.True(requestToken.IsCancellationRequested);
        }

        [Fact]
        public async Task HttpRetryHandler_ThrowsTimeoutExceptionForTimeout()
        {
            // Arrange
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler = async (requestMessage, token) =>
            {
                await Task.Delay(LargeTimeout);
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryHandler = new HttpRetryHandler();
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, TestUrl))
            {
                MaxTries = 1,
                RequestTimeout = TimeSpan.Zero,
                RetryDelay = TimeSpan.Zero
            };

            // Act
            Func<Task> actionAsync = () => retryHandler.SendAsync(
                request,
                new TestLogger(),
                CancellationToken.None);

            // Assert
            await Assert.ThrowsAsync<TimeoutException>(actionAsync);
        }

        [Fact]
        public async Task HttpRetryHandler_MultipleTriesTimed()
        {
            // Arrange
            Func<HttpRequestMessage, HttpResponseMessage> handler = requestMessage =>
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            };

            var minTime = GetRetryMinTime(MaxTries, SmallTimeout);

            var retryHandler = new HttpRetryHandler();
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, TestUrl))
            {
                MaxTries = MaxTries,
                RequestTimeout = Timeout.InfiniteTimeSpan,
                RetryDelay = SmallTimeout
            };
            var log = new TestLogger();

            // Act
            var timer = new Stopwatch();
            timer.Start();

            using (await retryHandler.SendAsync(request, log, CancellationToken.None))
            {
            }

            timer.Stop();

            // Assert
            Assert.True(
                timer.Elapsed >= minTime,
                $"Expected this to take at least: {minTime} But it finished in: {timer.Elapsed}");
        }

        [Fact]
        public async Task HttpRetryHandler_MultipleTriesNoSuccess()
        {
            // Arrange
            var hits = 0;

            Func<HttpRequestMessage, HttpResponseMessage> handler = requestMessage =>
            {
                hits++;

                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            };
            
            var retryHandler = new HttpRetryHandler();
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, TestUrl))
            {
                MaxTries = MaxTries,
                RequestTimeout = Timeout.InfiniteTimeSpan,
                RetryDelay = TimeSpan.Zero
            };
            var log = new TestLogger();

            // Act
            using (await retryHandler.SendAsync(request, log, CancellationToken.None))
            {
            }

            // Assert
            Assert.Equal(MaxTries, hits);
        }

        [Fact]
        public async Task HttpRetryHandler_MultipleSuccessFirstVerifySingleHit()
        {
            // Arrange
            var hits = 0;

            Func<HttpRequestMessage, HttpResponseMessage> handler = requestMessage =>
            {
                hits++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryHandler = new HttpRetryHandler();
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, TestUrl))
            {
                MaxTries = MaxTries,
                RequestTimeout = Timeout.InfiniteTimeSpan,
                RetryDelay = TimeSpan.Zero
            };
            var log = new TestLogger();

            // Act
            using (var response = await retryHandler.SendAsync(request, log, CancellationToken.None))
            {
                // Assert
                Assert.Equal(1, hits);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Fact]
        public async Task HttpRetryHandler_404VerifySingleHit()
        {
            // Arrange
            var hits = 0;

            Func<HttpRequestMessage, HttpResponseMessage> handler = requestMessage =>
            {
                hits++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryHandler = new HttpRetryHandler();
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, TestUrl))
            {
                MaxTries = MaxTries,
                RequestTimeout = Timeout.InfiniteTimeSpan,
                RetryDelay = TimeSpan.Zero
            };
            var log = new TestLogger();

            // Act
            using (var response = await retryHandler.SendAsync(request, log, CancellationToken.None))
            {
                // Assert
                Assert.Equal(1, hits);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Fact]
        public async Task HttpRetryHandler_TimesOutDownload()
        {
            // Arrange
            var hits = 0;
            var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes("foobar"));
            var expectedMilliseconds = 50;
            Func<HttpRequestMessage, HttpResponseMessage> handler = requestMessage =>
            {
                hits++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new SlowStream(memoryStream)
                    {
                        DelayPerByte = TimeSpan.FromSeconds(1)
                    })
                };
            };

            var retryHandler = new HttpRetryHandler();
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, TestUrl))
            {
                DownloadTimeout = TimeSpan.FromMilliseconds(expectedMilliseconds)
            };
            var destinationStream = new MemoryStream();
            var log = new TestLogger();

            // Act
            using (var response = await retryHandler.SendAsync(request, log, CancellationToken.None))
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var actual = await Assert.ThrowsAsync<IOException>(() => stream.CopyToAsync(destinationStream));

                // Assert
                Assert.Equal(1, hits);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.IsType<TimeoutException>(actual.InnerException);
                Assert.EndsWith(
                    $"timed out because no data was received for {expectedMilliseconds}ms.",
                    actual.Message);
            }
        }

        [Fact]
        public async Task HttpRetryHandler_MultipleTriesUntilSuccess()
        {
            // Arrange
            var tries = 0;
            var sent503 = false;

            Func<HttpRequestMessage, HttpResponseMessage> handler = requestMessage =>
            {
                tries++;

                // Return 503 for the first 2 tries
                if (tries > 2)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
                else
                {
                    sent503 = true;
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
            };

            var retryHandler = new HttpRetryHandler();
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, TestUrl))
            {
                MaxTries = MaxTries,
                RequestTimeout = Timeout.InfiniteTimeSpan,
                RetryDelay = TimeSpan.Zero
            };
            var log = new TestLogger();

            // Act
            using (var response = await retryHandler.SendAsync(request, log, CancellationToken.None))
            {
                // Assert
                Assert.True(sent503);
                Assert.Equal(3, tries);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        private static async Task<T> ThrowsException<T>(ITestServer server) where T : Exception
        {
            return await server.ExecuteAsync(async address =>
            {
                // Arrange
                var retryHandler = new HttpRetryHandler();
                var countingHandler = new CountingHandler { InnerHandler = new HttpClientHandler() };
                var httpClient = new HttpClient(countingHandler);
                var request = new HttpRetryHandlerRequest(httpClient, () => new HttpRequestMessage(HttpMethod.Get, address))
                {
                    MaxTries = 2,
                    RetryDelay = TimeSpan.Zero
                };

                // Act
                Func<Task> actionAsync = () => retryHandler.SendAsync(
                    request,
                    new TestLogger(),
                    CancellationToken.None);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<T>(actionAsync);
                Assert.Equal(2, countingHandler.Hits);
                return exception;
            });
        }

        private static TimeSpan GetRetryMinTime(int tries, TimeSpan retryDelay)
        {
            return TimeSpan.FromTicks((tries - 1) * retryDelay.Ticks);
        }

        private class CountingHandler : DelegatingHandler
        {
            public int Hits { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Hits++;

                return base.SendAsync(request, cancellationToken);
            }
        }

        private class TestHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public TestHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = (request, token) => Task.FromResult(handler(request));
            }

            public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return await _handler(request, cancellationToken);
            }
        }
    }
}
