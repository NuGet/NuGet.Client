// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Protocol;
using NuGet.Configuration;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class HttpSourceAuthenticationHandlerTests
    {
        [Fact]
        public void Constructor_WithSourceCredentials_InitializesClientHandler()
        {
            var packageSource = new PackageSource("http://package.source.net", "source")
            {
                Credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: true)
            };
            var clientHandler = new HttpClientHandler();
            var credentialService = Mock.Of<ICredentialService>();

            var handler = new HttpSourceAuthenticationHandler(packageSource, clientHandler, credentialService, false);

            Assert.NotNull(clientHandler.Credentials);

            var actualCredentials = clientHandler.Credentials.GetCredential(packageSource.SourceUri, "Basic");
            Assert.NotNull(actualCredentials);
            Assert.Equal("user", actualCredentials.UserName);
            Assert.Equal("password", actualCredentials.Password);
        }

        [Fact]
        public async Task SendAsync_WithUnauthenticatedSource_PassesThru()
        {
            var packageSource = new PackageSource("http://package.source.net");
            var clientHandler = new HttpClientHandler();

            var credentialService = new Mock<ICredentialService>(MockBehavior.Strict);
            credentialService.Setup(x => x.HandlesDefaultCredentialsAsync())
                .Returns(Task.FromResult(false));

            var handler = new HttpSourceAuthenticationHandler(packageSource, clientHandler, credentialService.Object, await credentialService.Object.HandlesDefaultCredentialsAsync())
            {
                InnerHandler = GetLambdaMessageHandler(HttpStatusCode.OK)
            };

            var response = await SendAsync(handler);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_WithAcquiredCredentialsOn401_RetriesRequest()
        {
            var packageSource = new PackageSource("http://package.source.net");
            var clientHandler = new HttpClientHandler();

            var credentialService = Mock.Of<ICredentialService>();
            Mock.Get(credentialService)
                .Setup(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Unauthorized,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ICredentials>(new NetworkCredential()));

            var handler = new HttpSourceAuthenticationHandler(packageSource, clientHandler, credentialService, false)
            {
                InnerHandler = GetLambdaMessageHandler(
                    HttpStatusCode.Unauthorized, HttpStatusCode.OK)
            };

            var response = await SendAsync(handler);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Mock.Get(credentialService)
                .Verify(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Unauthorized,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once());
        }

        [Fact]
        public async Task SendAsync_WithAcquiredCredentialsOn403_RetriesRequest()
        {
            // Arrange
            var packageSource = new PackageSource("http://package.source.net");
            var clientHandler = new HttpClientHandler();

            var credentialService = Mock.Of<ICredentialService>();
            Mock.Get(credentialService)
                .Setup(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Forbidden,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ICredentials>(new NetworkCredential()));

            var handler = new HttpSourceAuthenticationHandler(packageSource, clientHandler, credentialService, false)
            {
                InnerHandler = GetLambdaMessageHandler(
                    HttpStatusCode.Forbidden, HttpStatusCode.OK)
            };

            // Act
            var response = await SendAsync(handler);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Mock.Get(credentialService)
                .Verify(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Forbidden,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once());
        }

        [Fact]
        public async Task SendAsync_With403PromptDisabled_Returns403()
        {
            // Arrange
            var packageSource = new PackageSource("http://package.source.net");
            var clientHandler = new HttpClientHandler();

            var credentialService = new Mock<ICredentialService>(MockBehavior.Strict);
            credentialService.Setup(x => x.HandlesDefaultCredentialsAsync())
                .Returns(Task.FromResult(false));
            var handler = new HttpSourceAuthenticationHandler(packageSource, clientHandler, credentialService.Object, await credentialService.Object.HandlesDefaultCredentialsAsync())
            {
                InnerHandler = GetLambdaMessageHandler(HttpStatusCode.Forbidden)
            };

            var request = new HttpRequestMessage(HttpMethod.Get, "http://foo");
            request.SetConfiguration(new HttpRequestMessageConfiguration(promptOn403: false));

            // Act
            var response = await SendAsync(handler, request: request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_WhenTaskCanceledExceptionThrownDuringAcquiringCredentials_Throws()
        {
            // Arrange
            var packageSource = new PackageSource("http://package.source.net");
            var clientHandler = new HttpClientHandler();
            
            var credentialService = Mock.Of<ICredentialService>();
            Mock.Get(credentialService)
                .Setup(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Unauthorized,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TaskCanceledException());

            var handler = new HttpSourceAuthenticationHandler(packageSource, clientHandler, credentialService, false);

            int retryCount = 0;
            var innerHandler = new LambdaMessageHandler(
                _ =>
                {
                    retryCount++;
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                });
            handler.InnerHandler = innerHandler;

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(
                () => SendAsync(handler));

            Assert.Equal(1, retryCount);

            Mock.Get(credentialService)
                .Verify(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Unauthorized,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
        }

        [Fact]
        public async Task SendAsync_WhenOperationCanceledExceptionThrownDuringAcquiringCredentials_Throws()
        {
            // Arrange
            var packageSource = new PackageSource("http://package.source.net");
            var clientHandler = new HttpClientHandler();

            var cts = new CancellationTokenSource();

            var credentialService = Mock.Of<ICredentialService>();
            Mock.Get(credentialService)
                .Setup(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Unauthorized,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException())
                .Callback(() => cts.Cancel());

            var handler = new HttpSourceAuthenticationHandler(packageSource, clientHandler, credentialService, false);

            int retryCount = 0;
            var innerHandler = new LambdaMessageHandler(
                _ =>
                {
                    retryCount++;
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                });
            handler.InnerHandler = innerHandler;

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => SendAsync(handler));

            Assert.Equal(1, retryCount);

            Mock.Get(credentialService)
                .Verify(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Unauthorized,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
        }

        [Fact]
        public async Task SendAsync_WithWrongCredentials_StopsRetryingAfter3Times()
        {
            // Arrange
            var packageSource = new PackageSource("http://package.source.net");
            var clientHandler = new HttpClientHandler();

            var credentialService = Mock.Of<ICredentialService>();
            Mock.Get(credentialService)
                .Setup(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Unauthorized,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ICredentials>(new NetworkCredential()));

            var handler = new HttpSourceAuthenticationHandler(packageSource, clientHandler, credentialService, false);

            int retryCount = 0;
            var innerHandler = new LambdaMessageHandler(
                _ =>
                {
                    retryCount++;
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                });
            handler.InnerHandler = innerHandler;

            // Act
            var response = await SendAsync(handler);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            Assert.Equal(HttpSourceAuthenticationHandler.MaxAuthRetries + 1, retryCount);

            Mock.Get(credentialService)
                .Verify(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Unauthorized,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()),
                    Times.Exactly(HttpSourceAuthenticationHandler.MaxAuthRetries));
        }

        [Fact]
        public async Task SendAsync_WithMissingCredentials_Returns401()
        {
            // Arrange
            var packageSource = new PackageSource("http://package.source.net");
            var clientHandler = new HttpClientHandler();

            var credentialService = Mock.Of<ICredentialService>();

            var handler = new HttpSourceAuthenticationHandler(packageSource, clientHandler, credentialService, false);

            int retryCount = 0;
            var innerHandler = new LambdaMessageHandler(
                _ =>
                {
                    retryCount++;
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                });
            handler.InnerHandler = innerHandler;

            // Act
            var response = await SendAsync(handler);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            Assert.Equal(1, retryCount);

            Mock.Get(credentialService)
                .Verify(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Unauthorized,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once());
        }

        [Fact]
        public async Task SendAsync_WhenCredentialServiceThrows_Returns401()
        {
            // Arrange
            var packageSource = new PackageSource("http://package.source.net");
            var clientHandler = new HttpClientHandler();

            var credentialService = Mock.Of<ICredentialService>();
            Mock.Get(credentialService)
               .Setup(
                   x => x.GetCredentialsAsync(
                       packageSource.SourceUri,
                       It.IsAny<IWebProxy>(),
                       CredentialRequestType.Unauthorized,
                       It.IsAny<string>(),
                       It.IsAny<CancellationToken>()))
               .Throws(new InvalidOperationException("Credential service failed acquring user credentials"));

            var handler = new HttpSourceAuthenticationHandler(packageSource, clientHandler, credentialService, false);

            int retryCount = 0;
            var innerHandler = new LambdaMessageHandler(
                _ =>
                {
                    retryCount++;
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                });
            handler.InnerHandler = innerHandler;

            // Act
            var response = await SendAsync(handler);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            Assert.Equal(1, retryCount);

            Mock.Get(credentialService)
                .Verify(
                    x => x.GetCredentialsAsync(
                        packageSource.SourceUri,
                        It.IsAny<IWebProxy>(),
                        CredentialRequestType.Unauthorized,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once());
        }

        private static LambdaMessageHandler GetLambdaMessageHandler(HttpStatusCode statusCode)
        {
            return new LambdaMessageHandler(
                _ => new HttpResponseMessage(statusCode));
        }

        private static LambdaMessageHandler GetLambdaMessageHandler(params HttpStatusCode[] statusCodes)
        {
            var responses = new Queue<HttpStatusCode>(statusCodes);
            return new LambdaMessageHandler(
                _ => new HttpResponseMessage(responses.Dequeue()));
        }

        private static async Task<HttpResponseMessage> SendAsync(
            HttpMessageHandler handler,
            HttpRequestMessage request = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var client = new HttpClient(handler))
            {
                return await client.SendAsync(request ?? new HttpRequestMessage(HttpMethod.Get, "http://foo"), cancellationToken);
            }
        }
    }
}
