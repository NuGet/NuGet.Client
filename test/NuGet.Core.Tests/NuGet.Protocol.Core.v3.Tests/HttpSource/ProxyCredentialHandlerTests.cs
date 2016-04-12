using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Configuration;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class ProxyCredentialHandlerTests
    {
        private static readonly Uri ProxyAddress = new Uri("http://127.0.0.1:8888/");

        [Fact]
        public async Task SendAsync_WithUnauthenticatedProxy_PassesThru()
        {
            var defaultClientHandler = GetDefaultClientHandler();

            var service = Mock.Of<ICredentialService>();
            var handler = new ProxyCredentialHandler(defaultClientHandler, service, ProxyCache.Instance)
            {
                InnerHandler = GetLambdaHandler(HttpStatusCode.OK)
            };

            var response = await SendAsync(handler);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_WithMissingCredentials_Returns407()
        {
            var defaultClientHandler = GetDefaultClientHandler();

            var service = Mock.Of<ICredentialService>();
            var handler = new ProxyCredentialHandler(defaultClientHandler, service, ProxyCache.Instance)
            {
                InnerHandler = GetLambdaHandler(HttpStatusCode.ProxyAuthenticationRequired)
            };

            var response = await SendAsync(handler);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.ProxyAuthenticationRequired, response.StatusCode);

            Mock.Get(service).Verify(
                x => x.GetCredentialsAsync(
                    ProxyAddress,
                    It.IsAny<IWebProxy>(),
                    CredentialRequestType.Proxy,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                 Times.Once());
        }

        [Fact]
        public async Task SendAsync_WithAcquiredCredentials_RetriesRequest()
        {
            var defaultClientHandler = GetDefaultClientHandler();

            var service = Mock.Of<ICredentialService>();
            Mock.Get(service)
                .Setup(x => x.GetCredentialsAsync(
                    ProxyAddress,
                    It.IsAny<IWebProxy>(),
                    CredentialRequestType.Proxy,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ICredentials>(new NetworkCredential()));

            var handler = new ProxyCredentialHandler(defaultClientHandler, service, ProxyCache.Instance);

            var responses = new Queue<HttpStatusCode>(
                new[] { HttpStatusCode.ProxyAuthenticationRequired, HttpStatusCode.OK });
            var innerHandler = new LambdaMessageHandler(
                _ => new HttpResponseMessage(responses.Dequeue()));
            handler.InnerHandler = innerHandler;

            var response = await SendAsync(handler);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Mock.Get(service).Verify(
                x => x.GetCredentialsAsync(
                    ProxyAddress,
                    It.IsAny<IWebProxy>(),
                    CredentialRequestType.Proxy,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once());
        }

        [Fact]
        public async Task SendAsync_WithWrongCredentials_StopsRetryingAfter3Times()
        {
            var defaultClientHandler = GetDefaultClientHandler();

            var service = Mock.Of<ICredentialService>();
            Mock.Get(service)
                .Setup(x => x.GetCredentialsAsync(
                    ProxyAddress,
                    It.IsAny<IWebProxy>(),
                    CredentialRequestType.Proxy,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ICredentials>(new NetworkCredential()));

            var handler = new ProxyCredentialHandler(defaultClientHandler, service, ProxyCache.Instance);

            int retryCount = 0;
            var innerHandler = new LambdaMessageHandler(
                _ => { retryCount++; return new HttpResponseMessage(HttpStatusCode.ProxyAuthenticationRequired); });
            handler.InnerHandler = innerHandler;

            var response = await SendAsync(handler);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.ProxyAuthenticationRequired, response.StatusCode);

            Assert.Equal(ProxyCredentialHandler.MaxAuthRetries, retryCount);

            Mock.Get(service).Verify(
                x => x.GetCredentialsAsync(
                    ProxyAddress,
                    It.IsAny<IWebProxy>(),
                    CredentialRequestType.Proxy,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        private static HttpClientHandler GetDefaultClientHandler()
        {
            var proxy = new TestProxy(ProxyAddress);
            return new HttpClientHandler { Proxy = proxy };
        }

        private static LambdaMessageHandler GetLambdaHandler(HttpStatusCode statusCode)
        {
            return new LambdaMessageHandler(
                _ => new HttpResponseMessage(statusCode));
        }

        private static async Task<HttpResponseMessage> SendAsync(HttpMessageHandler handler, HttpRequestMessage request = null)
        {
            using (var client = new HttpClient(handler))
            {
                return await client.SendAsync(request ?? new HttpRequestMessage(HttpMethod.Get, "http://foo"));
            }
        }
    }
}
