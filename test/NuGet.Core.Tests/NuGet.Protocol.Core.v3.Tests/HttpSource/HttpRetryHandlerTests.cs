using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Test.Server;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class HttpRetryHandlerTests
    {
        private const int MaxTries = 5;
        private const string TestUrl = "https://test.local/test.json";
        private static readonly TimeSpan SmallTimeout = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan LargeTimeout = TimeSpan.FromMilliseconds(250);

        [Fact]
        public async Task HttpRetryHandler_HandlesFailureToConnect()
        {
            // https://github.com/NuGet/Home/issues/2096
            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                return;
            }

            // Arrange
            var server = new NotListeningServer { Mode = TestServerMode.ConnectFailure };

            // Act & Assert
            var exception = await ThrowsException<HttpRequestException>(server);
#if DNXCORE50
            Assert.NotNull(exception.InnerException);
            Assert.Equal("A connection with the server could not be established", exception.InnerException.Message);
#else
            var innerException = Assert.IsType<WebException>(exception.InnerException);
            Assert.Equal(WebExceptionStatus.ConnectFailure, innerException.Status);
#endif
        }

        [Fact]
        public async Task HttpRetryHandler_HandlesInvalidProtocol()
        {
            // https://github.com/NuGet/Home/issues/2096
            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                return;
            }

            // Arrange
            var server = new TcpListenerServer { Mode = TestServerMode.ServerProtocolViolation };

            // Act & Assert
            var exception = await ThrowsException<HttpRequestException>(server);
#if DNXCORE50
            Assert.NotNull(exception.InnerException);
            Assert.Equal("The server returned an invalid or unrecognized response", exception.InnerException.Message);
#else
            var innerException = Assert.IsType<WebException>(exception.InnerException);
            Assert.Equal(WebExceptionStatus.ServerProtocolViolation, innerException.Status);
#endif
        }

        [Fact]
        public async Task HttpRetryHandler_HandlesNameResolutionFailure()
        {
            // https://github.com/NuGet/Home/issues/2096
            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                return;
            }

            // Arrange
            var server = new UnknownDnsServer { Mode = TestServerMode.NameResolutionFailure };

            // Act & Assert
            var exception = await ThrowsException<HttpRequestException>(server);
#if DNXCORE50
            Assert.NotNull(exception.InnerException);
            Assert.Equal("The server name or address could not be resolved", exception.InnerException.Message);
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
            Func<HttpRequestMessage, HttpResponseMessage> handler = request =>
            {
                requests.Add(request);
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            };

            var retryHandler = new HttpRetryHandler { MaxTries = MaxTries, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            await retryHandler.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, TestUrl),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            Assert.Equal(MaxTries, requests.Count);
        }

        [Fact]
        public async Task HttpRetryHandler_AppliesTimeoutToRequestsIndividually()
        {
            // Arrange
            int hits = 0;

            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler = async (request, token) =>
            {
                hits++;
                await Task.Delay(SmallTimeout);
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            };

            var retryHandler = new HttpRetryHandler { MaxTries = MaxTries, RequestTimeout = LargeTimeout, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            var response = await retryHandler.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, TestUrl),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            Assert.Equal(MaxTries, hits);
        }

        [Fact]
        public async Task HttpRetryHandler_CancelsRequestAfterTimeout()
        {
            // Arrange
            CancellationToken requestToken = CancellationToken.None;
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler = async (request, token) =>
            {
                requestToken = token;
                await Task.Delay(LargeTimeout, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryHandler = new HttpRetryHandler { MaxTries = 1, RequestTimeout = SmallTimeout, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            Func<Task> actionAsync = () => retryHandler.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, TestUrl),
                HttpCompletionOption.ResponseHeadersRead,
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
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler = async (request, token) =>
            {
                await Task.Delay(LargeTimeout);
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryHandler = new HttpRetryHandler { MaxTries = 1, RequestTimeout = TimeSpan.Zero, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            Func<Task> actionAsync = () => retryHandler.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, TestUrl),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            await Assert.ThrowsAsync<TimeoutException>(actionAsync);
        }

        [Fact]
        public async Task HttpRetryHandler_MultipleTriesTimed()
        {
            // Arrange
            Func<HttpRequestMessage, HttpResponseMessage> handler = request =>
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            };

            var minTime = GetRetryMinTime(MaxTries, SmallTimeout);

            var retryHandler = new HttpRetryHandler { MaxTries = MaxTries, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = SmallTimeout };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            var timer = new Stopwatch();
            timer.Start();
            await retryHandler.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, TestUrl),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);
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

            Func<HttpRequestMessage, HttpResponseMessage> handler = request =>
            {
                hits++;

                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            };

            var retryHandler = new HttpRetryHandler { MaxTries = MaxTries, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            await retryHandler.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, TestUrl),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            Assert.Equal(MaxTries, hits);
        }

        [Fact]
        public async Task HttpRetryHandler_MultipleSuccessFirstVerifySingleHit()
        {
            // Arrange
            var hits = 0;

            Func<HttpRequestMessage, HttpResponseMessage> handler = request =>
            {
                hits++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryHandler = new HttpRetryHandler { MaxTries = MaxTries, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            var response = await retryHandler.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, TestUrl),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            Assert.Equal(1, hits);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task HttpRetryHandler_404VerifySingleHit()
        {
            // Arrange
            var hits = 0;

            Func<HttpRequestMessage, HttpResponseMessage> handler = request =>
            {
                hits++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryHandler = new HttpRetryHandler { MaxTries = MaxTries, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            var response = await retryHandler.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, TestUrl),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            Assert.Equal(1, hits);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task HttpRetryHandler_MultipleTriesUntilSuccess()
        {
            // Arrange
            var tries = 0;
            var sent503 = false;

            Func<HttpRequestMessage, HttpResponseMessage> handler = request =>
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

            var retryHandler = new HttpRetryHandler { MaxTries = MaxTries, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            var response = await retryHandler.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, TestUrl),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            Assert.True(sent503);
            Assert.Equal(3, tries);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private static async Task<T> ThrowsException<T>(ITestServer server) where T : Exception
        {
            return await server.ExecuteAsync(async address =>
            {
                // Arrange
                var retryHandler = new HttpRetryHandler { MaxTries = 2, RetryDelay = TimeSpan.Zero };
                var countingHandler = new CountingHandler { InnerHandler = new HttpClientHandler() };
                var httpClient = new HttpClient(countingHandler);

                // Act
                Func<Task> actionAsync = () => retryHandler.SendAsync(
                    httpClient,
                    () => new HttpRequestMessage(HttpMethod.Get, address),
                    HttpCompletionOption.ResponseHeadersRead,
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
