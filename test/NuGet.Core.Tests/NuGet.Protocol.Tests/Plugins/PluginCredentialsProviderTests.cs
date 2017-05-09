// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Configuration;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginCredentialsProviderTests
    {
        private readonly PackageSource _packageSource = new PackageSource("https://unit.test");

        [Fact]
        public void Constructor_ThrowsForNullPlugin()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new PluginCredentialsProvider(
                    plugin: null,
                    packageSource: _packageSource,
                    proxy: Mock.Of<IWebProxy>(),
                    credentialService: Mock.Of<ICredentialService>()));

            Assert.Equal("plugin", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullPackageSource()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new PluginCredentialsProvider(
                    Mock.Of<IPlugin>(),
                    packageSource: null,
                    proxy: Mock.Of<IWebProxy>(),
                    credentialService: Mock.Of<ICredentialService>()));

            Assert.Equal("packageSource", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullCredentialService()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new PluginCredentialsProvider(
                    Mock.Of<IPlugin>(),
                    _packageSource,
                    Mock.Of<IWebProxy>(),
                    credentialService: null));

            Assert.Equal("credentialService", exception.ParamName);
        }

        [Fact]
        public void CancellationToken_IsNone()
        {
            var provider = new PluginCredentialsProvider(
                Mock.Of<IPlugin>(),
                _packageSource,
                Mock.Of<IWebProxy>(),
                Mock.Of<ICredentialService>());

            Assert.Equal(CancellationToken.None, provider.CancellationToken);
        }

        [Fact]
        public void Dispose_DisposesDisposables()
        {
            var plugin = new Mock<IPlugin>(MockBehavior.Strict);

            plugin.Setup(x => x.Dispose());

            var provider = new PluginCredentialsProvider(
                plugin.Object,
                _packageSource,
                Mock.Of<IWebProxy>(),
                Mock.Of<ICredentialService>());

            provider.Dispose();

            plugin.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var plugin = new Mock<IPlugin>(MockBehavior.Strict);

            plugin.Setup(x => x.Dispose());

            var provider = new PluginCredentialsProvider(
                plugin.Object,
                _packageSource,
                Mock.Of<IWebProxy>(),
                Mock.Of<ICredentialService>());

            provider.Dispose();
            provider.Dispose();

            plugin.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public async Task HandleCancelAsync_Throws()
        {
            using (var provider = CreateDefaultPluginCredentialsProvider())
            {
                var request = CreateRequest(MessageType.Cancel);

                await Assert.ThrowsAsync<NotSupportedException>(
                    () => provider.HandleCancelAsync(
                        connection: Mock.Of<IConnection>(),
                        request: request,
                        responseHandler: Mock.Of<IResponseHandler>(),
                        cancellationToken: CancellationToken.None));
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ThrowsForNullConnection()
        {
            using (var provider = CreateDefaultPluginCredentialsProvider())
            {
                var request = CreateRequest(MessageType.Request);

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => provider.HandleResponseAsync(
                        connection: null,
                        request: request,
                        responseHandler: Mock.Of<IResponseHandler>(),
                        cancellationToken: CancellationToken.None));

                Assert.Equal("connection", exception.ParamName);
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ThrowsForNullRequest()
        {
            using (var provider = CreateDefaultPluginCredentialsProvider())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => provider.HandleResponseAsync(
                        connection: Mock.Of<IConnection>(),
                        request: null,
                        responseHandler: Mock.Of<IResponseHandler>(),
                        cancellationToken: CancellationToken.None));

                Assert.Equal("request", exception.ParamName);
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ThrowsForNullResponseHandler()
        {
            using (var provider = CreateDefaultPluginCredentialsProvider())
            {
                var request = CreateRequest(MessageType.Request);

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => provider.HandleResponseAsync(
                        connection: Mock.Of<IConnection>(),
                        request: request,
                        responseHandler: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("responseHandler", exception.ParamName);
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ThrowsIfCancelled()
        {
            using (var provider = CreateDefaultPluginCredentialsProvider())
            {
                var request = CreateRequest(MessageType.Request);

                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => provider.HandleResponseAsync(
                        Mock.Of<IConnection>(),
                        request,
                        Mock.Of<IResponseHandler>(),
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ReturnsNotFoundForNonHttpSource()
        {
            var packageSource = new PackageSource("\\unit\test");

            using (var provider = new PluginCredentialsProvider(
                Mock.Of<IPlugin>(),
                packageSource,
                Mock.Of<IWebProxy>(),
                Mock.Of<ICredentialService>()))
            {
                var request = CreateRequest(
                    MessageType.Request,
                    new GetCredentialsRequest(packageSource.Source, HttpStatusCode.Unauthorized));
                var responseHandler = new Mock<IResponseHandler>(MockBehavior.Strict);

                responseHandler.Setup(x => x.SendResponseAsync(
                        It.Is<Message>(r => r == request),
                        It.Is<GetCredentialsResponse>(r => r.ResponseCode == MessageResponseCode.NotFound
                            && r.Username == null && r.Password == null),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(0));

                await provider.HandleResponseAsync(
                    Mock.Of<IConnection>(),
                    request,
                    responseHandler.Object,
                    CancellationToken.None);

                responseHandler.Verify();
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ReturnsNotFoundForNonMatchingHttpSource()
        {
            var packageSource = new PackageSource("https://unit1.test");

            using (var provider = new PluginCredentialsProvider(
                Mock.Of<IPlugin>(),
                packageSource,
                Mock.Of<IWebProxy>(),
                Mock.Of<ICredentialService>()))
            {
                var request = CreateRequest(
                    MessageType.Request,
                    new GetCredentialsRequest("https://unit2.test", HttpStatusCode.Unauthorized));
                var responseHandler = new Mock<IResponseHandler>(MockBehavior.Strict);

                responseHandler.Setup(x => x.SendResponseAsync(
                        It.Is<Message>(r => r == request),
                        It.Is<GetCredentialsResponse>(r => r.ResponseCode == MessageResponseCode.NotFound
                            && r.Username == null && r.Password == null),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(0));

                await provider.HandleResponseAsync(
                    Mock.Of<IConnection>(),
                    request,
                    responseHandler.Object,
                    CancellationToken.None);

                responseHandler.Verify();
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ReturnsPackageSourceCredentialsFromPackageSourceIfValid()
        {
            _packageSource.Credentials = new PackageSourceCredential(
                _packageSource.Source,
                username: "a",
                passwordText: "b",
                isPasswordClearText: true);

            using (var provider = CreateDefaultPluginCredentialsProvider())
            {
                var request = CreateRequest(
                    MessageType.Request,
                    new GetCredentialsRequest(_packageSource.Source, HttpStatusCode.Unauthorized));
                var responseHandler = new Mock<IResponseHandler>(MockBehavior.Strict);

                responseHandler.Setup(x => x.SendResponseAsync(
                        It.Is<Message>(r => r == request),
                        It.Is<GetCredentialsResponse>(r => r.ResponseCode == MessageResponseCode.Success
                            && r.Username == "a" && r.Password == "b"),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(0));

                await provider.HandleResponseAsync(
                    Mock.Of<IConnection>(),
                    request,
                    responseHandler.Object,
                    CancellationToken.None);

                responseHandler.Verify();
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ReturnsPackageSourceCredentialsFromCredentialServiceIfFound()
        {
            var networkCredential = new NetworkCredential(userName: "a", password: "b");
            var credentialService = new Mock<ICredentialService>();
            var credentials = new Mock<ICredentials>();
            var proxy = Mock.Of<IWebProxy>();

            credentials.Setup(x => x.GetCredential(
                    It.Is<Uri>(u => u == _packageSource.SourceUri),
                    It.Is<string>(a => a == null)))
                .Returns(networkCredential);

            credentialService.Setup(x => x.GetCredentialsAsync(
                    It.Is<Uri>(uri => uri == _packageSource.SourceUri),
                    It.Is<IWebProxy>(p => p == proxy),
                    It.Is<CredentialRequestType>(t => t == CredentialRequestType.Unauthorized),
                    It.IsNotNull<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(credentials.Object));

            using (var provider = new PluginCredentialsProvider(
                Mock.Of<IPlugin>(),
                _packageSource,
                proxy,
                credentialService.Object))
            {
                var request = CreateRequest(
                    MessageType.Request,
                    new GetCredentialsRequest(_packageSource.Source, HttpStatusCode.Unauthorized));
                var responseHandler = new Mock<IResponseHandler>(MockBehavior.Strict);

                responseHandler.Setup(x => x.SendResponseAsync(
                        It.Is<Message>(r => r == request),
                        It.Is<GetCredentialsResponse>(r => r.ResponseCode == MessageResponseCode.Success
                            && r.Username == "a" && r.Password == "b"),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(0));

                await provider.HandleResponseAsync(
                    Mock.Of<IConnection>(),
                    request,
                    responseHandler.Object,
                    CancellationToken.None);

                responseHandler.Verify();
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ReturnsNullPackageSourceCredentialsIfNoCredentials()
        {
            var credentialService = new Mock<ICredentialService>();
            var credentials = new Mock<ICredentials>();
            var proxy = Mock.Of<IWebProxy>();

            credentials.Setup(x => x.GetCredential(
                    It.Is<Uri>(u => u == _packageSource.SourceUri),
                    It.Is<string>(a => a == null)))
                .Returns<NetworkCredential>(null);

            credentialService.Setup(x => x.GetCredentialsAsync(
                    It.Is<Uri>(uri => uri == _packageSource.SourceUri),
                    It.Is<IWebProxy>(p => p == proxy),
                    It.Is<CredentialRequestType>(t => t == CredentialRequestType.Unauthorized),
                    It.IsNotNull<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(credentials.Object));

            using (var provider = new PluginCredentialsProvider(
                Mock.Of<IPlugin>(),
                _packageSource,
                proxy,
                credentialService.Object))
            {
                var request = CreateRequest(
                    MessageType.Request,
                    new GetCredentialsRequest(_packageSource.Source, HttpStatusCode.Unauthorized));
                var responseHandler = new Mock<IResponseHandler>(MockBehavior.Strict);

                responseHandler.Setup(x => x.SendResponseAsync(
                        It.Is<Message>(r => r == request),
                        It.Is<GetCredentialsResponse>(r => r.ResponseCode == MessageResponseCode.NotFound
                            && r.Username == null && r.Password == null),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(0));

                await provider.HandleResponseAsync(
                    Mock.Of<IConnection>(),
                    request,
                    responseHandler.Object,
                    CancellationToken.None);

                responseHandler.Verify();
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ReturnsProxyCredentialsFromCredentialServiceIfFound()
        {
            var proxyUri = new Uri("https://proxy.unit.test");
            var networkCredential = new NetworkCredential(userName: "a", password: "b");
            var credentialService = new Mock<ICredentialService>();
            var credentials = new Mock<ICredentials>();
            var proxy = new Mock<IWebProxy>();

            proxy.Setup(x => x.GetProxy(It.IsAny<Uri>()))
                .Returns(proxyUri);

            credentials.Setup(x => x.GetCredential(
                    It.Is<Uri>(u => u == proxyUri),
                    It.Is<string>(a => a == "Basic")))
                .Returns(networkCredential);

            credentialService.Setup(x => x.GetCredentialsAsync(
                    It.Is<Uri>(u => u == _packageSource.SourceUri),
                    It.Is<IWebProxy>(p => p == proxy.Object),
                    It.Is<CredentialRequestType>(t => t == CredentialRequestType.Proxy),
                    It.IsNotNull<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(credentials.Object));

            using (var provider = new PluginCredentialsProvider(
                Mock.Of<IPlugin>(),
                _packageSource,
                proxy.Object,
                credentialService.Object))
            {
                var request = CreateRequest(
                    MessageType.Request,
                    new GetCredentialsRequest(_packageSource.Source, HttpStatusCode.ProxyAuthenticationRequired));
                var responseHandler = new Mock<IResponseHandler>(MockBehavior.Strict);

                responseHandler.Setup(x => x.SendResponseAsync(
                        It.Is<Message>(r => r == request),
                        It.Is<GetCredentialsResponse>(r => r.ResponseCode == MessageResponseCode.Success
                            && r.Username == "a" && r.Password == "b"),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(0));

                await provider.HandleResponseAsync(
                    Mock.Of<IConnection>(),
                    request,
                    responseHandler.Object,
                    CancellationToken.None);

                responseHandler.Verify();
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ReturnsNullProxyCredentialsIfNoCredentials()
        {
            var credentialService = new Mock<ICredentialService>();
            var credentials = new Mock<ICredentials>();
            var proxy = Mock.Of<IWebProxy>();

            credentials.Setup(x => x.GetCredential(
                    It.Is<Uri>(u => u == _packageSource.SourceUri),
                    It.Is<string>(a => a == null)))
                .Returns<NetworkCredential>(null);

            credentialService.Setup(x => x.GetCredentialsAsync(
                    It.Is<Uri>(uri => uri == _packageSource.SourceUri),
                    It.Is<IWebProxy>(p => p == proxy),
                    It.Is<CredentialRequestType>(t => t == CredentialRequestType.Proxy),
                    It.IsNotNull<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(credentials.Object));

            using (var provider = new PluginCredentialsProvider(
                Mock.Of<IPlugin>(),
                _packageSource,
                proxy,
                credentialService.Object))
            {
                var request = CreateRequest(
                    MessageType.Request,
                    new GetCredentialsRequest(_packageSource.Source, HttpStatusCode.ProxyAuthenticationRequired));
                var responseHandler = new Mock<IResponseHandler>(MockBehavior.Strict);

                responseHandler.Setup(x => x.SendResponseAsync(
                        It.Is<Message>(r => r == request),
                        It.Is<GetCredentialsResponse>(r => r.ResponseCode == MessageResponseCode.NotFound
                            && r.Username == null && r.Password == null),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(0));

                await provider.HandleResponseAsync(
                    Mock.Of<IConnection>(),
                    request,
                    responseHandler.Object,
                    CancellationToken.None);

                responseHandler.Verify();
            }
        }

        [Fact]
        public async Task HandleResponseAsync_ReturnsNullProxyCredentialsIfNoProxy()
        {
            using (var provider = new PluginCredentialsProvider(
                Mock.Of<IPlugin>(),
                _packageSource,
                proxy: null,
                credentialService: Mock.Of<ICredentialService>()))
            {
                var request = CreateRequest(
                    MessageType.Request,
                    new GetCredentialsRequest(_packageSource.Source, HttpStatusCode.ProxyAuthenticationRequired));
                var responseHandler = new Mock<IResponseHandler>(MockBehavior.Strict);

                responseHandler.Setup(x => x.SendResponseAsync(
                        It.Is<Message>(r => r == request),
                        It.Is<GetCredentialsResponse>(r => r.ResponseCode == MessageResponseCode.NotFound
                            && r.Username == null && r.Password == null),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(0));

                await provider.HandleResponseAsync(
                    Mock.Of<IConnection>(),
                    request,
                    responseHandler.Object,
                    CancellationToken.None);

                responseHandler.Verify();
            }
        }

        private PluginCredentialsProvider CreateDefaultPluginCredentialsProvider()
        {
            return new PluginCredentialsProvider(
                Mock.Of<IPlugin>(),
                _packageSource,
                Mock.Of<IWebProxy>(),
                Mock.Of<ICredentialService>());
        }

        private static Message CreateRequest(MessageType type, GetCredentialsRequest payload = null)
        {
            if (payload == null)
            {
                return new Message(
                    requestId: "a",
                    type: type,
                    method: MessageMethod.GetCredentials,
                    payload: null);
            }

            return MessageUtilities.Create(
                requestId: "a",
                type: MessageType.Request,
                method: MessageMethod.GetCredentials,
                payload: payload);
        }
    }
}