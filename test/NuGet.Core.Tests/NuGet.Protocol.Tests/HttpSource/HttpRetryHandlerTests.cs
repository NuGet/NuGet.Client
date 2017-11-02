// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Test.Utility;
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
        public async Task HttpRetryHandler_ReturnsContentHeaders()
        {
            // Arrange
            Func<HttpRequestMessage, HttpResponseMessage> handler = requestMessage =>
            {
                var stringContent = new StringContent("Plain text document.", Encoding.UTF8, "text/plain");
                stringContent.Headers.TryAddWithoutValidation("X-Content-ID", "49f47c14-c21f-4c1d-9e13-4f5fcf5f8013");
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = stringContent,
                };
                response.Headers.TryAddWithoutValidation("X-Message-Header", "This isn't on the content.");
                return response;
            };

            var retryHandler = new HttpRetryHandler();
            var testHandler = new HttpRetryTestHandler(handler);
            var httpClient = new HttpClient(testHandler);
            var request = new HttpRetryHandlerRequest(
                httpClient,
                () => new HttpRequestMessage(HttpMethod.Get, TestUrl));
            var log = new TestLogger();

            // Act
            var actualResponse = await retryHandler.SendAsync(request, log, CancellationToken.None);

            // Assert
            var actualHeaders = actualResponse
                .Content
                .Headers
                .OrderBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Value);
            Assert.Equal(
                new List<string> { "Content-Type", "X-Content-ID" },
                actualHeaders.Keys.OrderBy(x => x).ToList());
            Assert.Equal(actualHeaders["Content-Type"], new[] { "text/plain; charset=utf-8" });
            Assert.Equal(actualHeaders["X-Content-ID"], new[] { "49f47c14-c21f-4c1d-9e13-4f5fcf5f8013" });
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
            var testHandler = new HttpRetryTestHandler(handler);
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
        public async Task HttpRetryHandler_CancelsRequestAfterTimeout()
        {
            // Arrange
            var requestToken = CancellationToken.None;
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler = async (requestMessage, token) =>
            {
                requestToken = token;
                await Task.Delay(LargeTimeout, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var retryHandler = new HttpRetryHandler();
            var testHandler = new HttpRetryTestHandler(handler);
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
            var testHandler = new HttpRetryTestHandler(handler);
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
            var testHandler = new HttpRetryTestHandler(handler);
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
            var testHandler = new HttpRetryTestHandler(handler);
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
            var testHandler = new HttpRetryTestHandler(handler);
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
            var testHandler = new HttpRetryTestHandler(handler);
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
            var testHandler = new HttpRetryTestHandler(handler);
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
            var testHandler = new HttpRetryTestHandler(handler);
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

        private static TimeSpan GetRetryMinTime(int tries, TimeSpan retryDelay)
        {
            return TimeSpan.FromTicks((tries - 1) * retryDelay.Ticks);
        }
    }
}
