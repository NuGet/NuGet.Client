using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class RetryHandlerTests
    {
        [Fact]
        public async Task RetryHandler_HttpHandlerResourceV3IncludesRetryHandler()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3("https://test.local/test.json");

            // Act
            var handler = await repo.GetResourceAsync<HttpHandlerResource>();

            var messageHandler = handler.MessageHandler as HttpMessageHandler;

            // Assert
            Assert.True(messageHandler is RetryHandler);
        }

        [Fact]
        public async Task RetryHandler_MessageHandlerMultipleTriesTimed()
        {
            // Arrange
            Func<HttpRequestMessage, HttpResponseMessage> handler = (request) =>
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            };

            var testHandler = new TestHandler(handler);
            var retryHandler = new RetryHandler(testHandler, 5);

            var httpClient = new HttpClient(retryHandler);

            var minTime = GetRetryMinTime(retryHandler.MaxTries, retryHandler);

            // Act
            var timer = new Stopwatch();
            timer.Start();
            var response = await httpClient.GetAsync("https://test.local/test.json");
            timer.Stop();

            // Assert
            Assert.True(timer.Elapsed >= minTime, 
                string.Format("Expected this to take at least: {0} But it finished in: {1}",
                    minTime,
                    timer.Elapsed));
        }

        [Fact]
        public async Task RetryHandler_MessageHandlerMultipleTriesNoSuccess()
        {
            // Arrange
            var hits = 0;

            Func<HttpRequestMessage, HttpResponseMessage> handler = (request) =>
            {
                hits++;

                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            };

            var testHandler = new TestHandler(handler);
            var retryHandler = new RetryHandler(testHandler, 5);

            var httpClient = new HttpClient(retryHandler);

            // Act
            var response = await httpClient.GetAsync("https://test.local/test.json");
            var triesCount = retryHandler.MaxTries;

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(5, hits);
            Assert.Equal(5, triesCount);
        }

        [Fact]
        public async Task RetryHandler_MessageHandlerMultipleSuccessFirstVerifySingleHit()
        {
            // Arrange
            var hits = 0;

            Func<HttpRequestMessage, HttpResponseMessage> handler = (request) =>
            {
                hits++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var testHandler = new TestHandler(handler);
            var retryHandler = new RetryHandler(testHandler, 10);

            var httpClient = new HttpClient(retryHandler);

            // Act
            var response = await httpClient.GetAsync("https://test.local/test.json");

            // Assert
            Assert.Equal(1, hits);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task RetryHandler_MessageHandler404VerifySingleHit()
        {
            // Arrange
            var hits = 0;

            Func<HttpRequestMessage, HttpResponseMessage> handler = (request) =>
            {
                hits++;
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            };

            var testHandler = new TestHandler(handler);
            var retryHandler = new RetryHandler(testHandler, 10);

            var httpClient = new HttpClient(retryHandler);

            // Act
            var response = await httpClient.GetAsync("https://test.local/test.json");

            // Assert
            Assert.Equal(1, hits);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RetryHandler_MessageHandlerMultipleTriesUntilSuccess()
        {
            // Arrange
            var tries = 0;
            var sent503 = false;

            Func<HttpRequestMessage, HttpResponseMessage> handler = (request) =>
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

            var testHandler = new TestHandler(handler);
            var retryHandler = new RetryHandler(testHandler, 5);
            var minTime = GetRetryMinTime(2, retryHandler);

            var httpClient = new HttpClient(retryHandler);

            // Act
            var timer = new Stopwatch();
            timer.Start();
            var response = await httpClient.GetAsync("https://test.local/test.json");
            timer.Stop();

            // Assert
            Assert.True(sent503);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(timer.Elapsed >= minTime,
                string.Format("Expected this to take at least: {0} But it finished in: {1}",
                    minTime,
                    timer.Elapsed));
        }

        private static TimeSpan GetRetryMinTime(int tries, RetryHandler handler)
        {
            return TimeSpan.FromMilliseconds((tries - 1) * handler.RetryDelay.Milliseconds);
        }

        private class TestHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public TestHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<HttpResponseMessage>(_handler(request));
            }
        }
    }
}
