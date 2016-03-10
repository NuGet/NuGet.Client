using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
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

            var driver = Mock.Of<IProxyCredentialDriver>();
            var handler = new ProxyCredentialHandler(defaultClientHandler, driver);

            var innerHandler = GetLambdaHandler(HttpStatusCode.OK);
            handler.InnerHandler = innerHandler;

            var request = new HttpRequestMessage(HttpMethod.Get, "http://foo");

            using (var client = new HttpClient(handler))
            {
                var response = await client.SendAsync(request);

                Assert.NotNull(response);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Fact]
        public async Task SendAsync_WithMissingCredentials_Returns407()
        {
            var defaultClientHandler = GetDefaultClientHandler();

            var driver = Mock.Of<IProxyCredentialDriver>();
            var handler = new ProxyCredentialHandler(defaultClientHandler, driver);

            var innerHandler = GetLambdaHandler(HttpStatusCode.ProxyAuthenticationRequired);
            handler.InnerHandler = innerHandler;

            var request = new HttpRequestMessage(HttpMethod.Get, "http://foo");

            using (var client = new HttpClient(handler))
            {
                var response = await client.SendAsync(request);

                Mock.Get(driver)
                    .Verify(x => x.AcquireCredentialsAsync(ProxyAddress, It.IsAny<IWebProxy>(), It.IsAny<CancellationToken>()), Times.Once());

                Assert.NotNull(response);
                Assert.Equal(HttpStatusCode.ProxyAuthenticationRequired, response.StatusCode);
            }
        }

        [Fact]
        public async Task SendAsync_WithAcquiredCredentials_RetriesRequest()
        {
            var defaultClientHandler = GetDefaultClientHandler();

            var driver = Mock.Of<IProxyCredentialDriver>();
            Mock.Get(driver)
                .Setup(x => x.AcquireCredentialsAsync(ProxyAddress, It.IsAny<IWebProxy>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(new NetworkCredential()));

            var handler = new ProxyCredentialHandler(defaultClientHandler, driver);

            var responses = new Queue<HttpStatusCode>(
                new[] { HttpStatusCode.ProxyAuthenticationRequired, HttpStatusCode.OK });
            var innerHandler = new LambdaMessageHandler(
                _ => new HttpResponseMessage(responses.Dequeue()));
            handler.InnerHandler = innerHandler;

            var request = new HttpRequestMessage(HttpMethod.Get, "http://foo");

            using (var client = new HttpClient(handler))
            {
                var response = await client.SendAsync(request);

                Mock.Get(driver)
                    .Verify(x => x.AcquireCredentialsAsync(ProxyAddress, It.IsAny<IWebProxy>(), It.IsAny<CancellationToken>()), Times.Once());

                Assert.NotNull(response);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
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
    }
}
