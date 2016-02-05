using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class HttpRetryHandlerTests
    {
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

            var retryLoop = new HttpRetryHandler { MaxTries = 5, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            await retryLoop.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, "https://test.local/test.json"),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            Assert.Equal(5, requests.Count);
        }

        [Fact]
        public async Task HttpRetryHandler_AppliesTimeoutToRequestsIndividually()
        {
            // Arrange
            int hits = 0;

            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler = async (request, token) =>
            {
                hits++;
                await Task.Delay(TimeSpan.FromMilliseconds(25));
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            };

            var retryLoop = new HttpRetryHandler { MaxTries = 5, RequestTimeout = TimeSpan.FromMilliseconds(100), RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            var response = await retryLoop.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, "https://test.local/test.json"),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            Assert.Equal(5, hits);
        }

        [Fact]
        public async Task HttpRetryHandler_CancelsRequestAfterTimeout()
        {
            // Arrange
            CancellationToken requestToken = CancellationToken.None;
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler = async (request, token) =>
            {
                requestToken = token;
                await Task.Delay(TimeSpan.FromMilliseconds(200), token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryLoop = new HttpRetryHandler { MaxTries = 1, RequestTimeout = TimeSpan.FromMilliseconds(100), RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            Func<Task> actionAsync = () => retryLoop.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, "https://test.local/test.json"),
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
                await Task.Delay(TimeSpan.FromMilliseconds(50));
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryLoop = new HttpRetryHandler { MaxTries = 1, RequestTimeout = TimeSpan.Zero, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            Func<Task> actionAsync = () => retryLoop.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, "https://test.local/test.json"),
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

            var maxTries = 5;
            var retryDelay = TimeSpan.FromMilliseconds(50);
            var minTime = GetRetryMinTime(maxTries, retryDelay);

            var retryLoop = new HttpRetryHandler { MaxTries = maxTries, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = retryDelay };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            var timer = new Stopwatch();
            timer.Start();
            await retryLoop.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, "https://test.local/test.json"),
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

            var retryLoop = new HttpRetryHandler { MaxTries = 5, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            await retryLoop.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, "https://test.local/test.json"),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            Assert.Equal(5, hits);
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

            var retryLoop = new HttpRetryHandler { MaxTries = 5, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            var response = await retryLoop.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, "https://test.local/test.json"),
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

            var retryLoop = new HttpRetryHandler { MaxTries = 5, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            var response = await retryLoop.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, "https://test.local/test.json"),
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

            var retryLoop = new HttpRetryHandler { MaxTries = 5, RequestTimeout = Timeout.InfiniteTimeSpan, RetryDelay = TimeSpan.Zero };
            var testHandler = new TestHandler(handler);
            var httpClient = new HttpClient(testHandler);

            // Act
            var response = await retryLoop.SendAsync(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, "https://test.local/test.json"),
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None);

            // Assert
            Assert.True(sent503);
            Assert.Equal(3, tries);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private static TimeSpan GetRetryMinTime(int tries, TimeSpan retryDelay)
        {
            return TimeSpan.FromTicks((tries - 1) * retryDelay.Ticks);
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
