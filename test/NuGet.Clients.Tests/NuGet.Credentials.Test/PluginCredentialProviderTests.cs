using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Configuration;
using Xunit;

namespace NuGet.Credentials.Test
{
    public class PluginCredentialProviderTests
    {
        private readonly Mock<PluginCredentialProvider> _mockProvider;
        private PluginCredentialRequest _actualRequest;

        public PluginCredentialProviderTests()
        {
            _mockProvider = new Mock<PluginCredentialProvider>(@"c:\path\plugin.exe", 10) {CallBase = true};
            _mockProvider.Setup(x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()))
                .Callback((PluginCredentialRequest x, CancellationToken y) =>_actualRequest=x)
                .Returns(new PluginCredentialResponse());
        }

        public PluginCredentialProvider CreatePlugin()
        {
            return _mockProvider.Object;
        }

        [Fact]
        public async Task WhenProxyRequest_ReturnNull()
        {
            // Arrange
            var provider = _mockProvider;
            var proxy = null as IWebProxy;
            var uri = new Uri("http://host/");
            var type = CredentialRequestType.Proxy;
            var message = null as string;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var result = await provider.Object.GetAsync(
                uri,
                proxy,
                type,
                message,
                isRetry,
                nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.Equal(CredentialStatus.ProviderNotApplicable, result.Status);
            Assert.Null(result.Credentials);
            _mockProvider.Verify(
                x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()), 
                Times.Never());
        }

        [Fact]
        public async Task CreatesExpectedCredentialRequestWithUnauthorized()
        {
            // Arrange
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var proxy = null as IWebProxy;
            var type = CredentialRequestType.Unauthorized;
            var message = null as string;
            var isRetry = true;
            var nonInteractive = true;

            // Act
            var result = await provider.Object.GetAsync(
                uri,
                proxy,
                type,
                message,
                isRetry,
                nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.Equal(CredentialStatus.ProviderNotApplicable, result.Status);
            Assert.Null(result.Credentials);
            Assert.Equal("http://host/", _actualRequest.Uri);
            Assert.True(_actualRequest.IsRetry);
            Assert.True(_actualRequest.NonInteractive);
        }

        [Fact]
        public async Task CreatesExpectedCredentialRequestWithForbidden()
        {
            // Arrange
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var proxy = null as IWebProxy;
            var type = CredentialRequestType.Forbidden;
            var message = null as string;
            var isRetry = true;
            var nonInteractive = true;

            // Act
            var result = await provider.Object.GetAsync(
                uri,
                proxy,
                type,
                message,
                isRetry,
                nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.Equal(CredentialStatus.ProviderNotApplicable, result.Status);
            Assert.Null(result.Credentials);
            Assert.Equal("http://host/", _actualRequest.Uri);
            Assert.True(_actualRequest.IsRetry);
            Assert.True(_actualRequest.NonInteractive);
        }

        [Fact]
        public async Task WhenResponseContainsAbort_ThenThrow()
        {
            // Arrange
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var proxy = null as IWebProxy;
            var type = CredentialRequestType.Unauthorized;
            var message = null as string;
            var isRetry = true;
            var nonInteractive = true;
            _mockProvider.Setup(x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()))
                .Throws(PluginException.CreateAbortMessage(@"c:\path\plugin.exe", ""));

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () => await provider.Object.GetAsync(
                uri,
                proxy,
                type,
                message,
                isRetry,
                nonInteractive,
                CancellationToken.None));

            Assert.IsAssignableFrom<PluginException>(exception);
            Assert.Contains(@"Credential plugin c:\path\plugin.exe handles this request, but is unable to provide credentials.", 
                exception.Message);
        }

        [Fact]
        public async Task WhenResponseContainsAbortAndAbortMessage_ThenThrow()
        {
            // Arrange
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var proxy = null as IWebProxy;
            var type = CredentialRequestType.Unauthorized;
            var message = null as string;
            var isRetry = true;
            var nonInteractive = true;
            _mockProvider.Setup(x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()))
                .Throws(PluginException.CreateAbortMessage(@"c:\path\plugin.exe", "Extra message."));

            // Act & Assert
            var exception =  await Record.ExceptionAsync(async () => await provider.Object.GetAsync(
                uri,
                proxy,
                type,
                message,
                isRetry,
                nonInteractive,
                CancellationToken.None));

            Assert.IsAssignableFrom<PluginException>(exception);
            Assert.Contains(
                @"Credential plugin c:\path\plugin.exe handles this request, but is unable to provide credentials. Extra message.",
                exception.Message);
        }

        [Fact]
        public async Task WhenResponseContainsUsername_ReturnCredential()
        {
            // Arrange
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var proxy = null as IWebProxy;
            var type = CredentialRequestType.Unauthorized;
            var message = null as string;
            var isRetry = true;
            var nonInteractive = true;
            _mockProvider.Setup(x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()))
                .Returns(new PluginCredentialResponse() { Username = "u" });

            // Act
            var result = await provider.Object.GetAsync(
                uri,
                proxy,
                type,
                message,
                isRetry,
                nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Credentials);
            Assert.Equal("u", ((NetworkCredential)result.Credentials)?.UserName);
        }

        [Fact]
        public async Task WhenResponseContainsPassword_ReturnCredential()
        {
            // Arrange
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var proxy = null as IWebProxy;
            var type = CredentialRequestType.Unauthorized;
            var message = null as string;
            var isRetry = true;
            var nonInteractive = true;
            _mockProvider.Setup(x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()))
                .Returns(new PluginCredentialResponse() { Password = "p" });

            // Act
            var result = await provider.Object.GetAsync(
                uri,
                proxy,
                type,
                message,
                isRetry,
                nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Credentials);
            Assert.Equal("p", ((NetworkCredential)result.Credentials)?.Password);
        }

        [Fact]
        public async Task WhenCredentialProviderIsCanceled_Throws()
        {
            // Arrange
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var proxy = null as IWebProxy;
            var type = CredentialRequestType.Unauthorized;
            var message = null as string;
            var isRetry = true;
            var nonInteractive = true;
            var exception = new OperationCanceledException();
            _mockProvider
                .Setup(x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()))
                .Throws(exception);

            // Act & Assert
            var actual = await Assert.ThrowsAsync<OperationCanceledException>(() => provider.Object.GetAsync(
                uri,
                proxy,
                type,
                message,
                isRetry,
                nonInteractive,
                CancellationToken.None));
            Assert.Same(exception, actual);
        }

        [Fact]
        public void SetsIdBasedOnTypeAndFilename()
        {
            var provider = new PluginCredentialProvider(@"c:\some\path\provider.exe", 5);

            Assert.StartsWith("PluginCredentialProvider_provider.exe_", provider.Id);
        }
    }
}
