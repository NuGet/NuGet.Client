using System;
using System.Net;
using System.Threading;
using Moq;
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
        public async void WhenProxyRequest_ReturnNull()
        {
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var isProxyRequest = true;
            var isRetry = false;
            var nonInteractive = false;

            var result = await provider.Object.Get(
                uri, null, isProxyRequest, isRetry, nonInteractive, CancellationToken.None);

            Assert.Null(result);
            _mockProvider.Verify(
                x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()), 
                Times.Never());
        }

        [Fact]
        public async void CreatesExpectedCredentialRequest()
        {
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var isProxyRequest = false;
            var isRetry = true;
            var nonInteractive = true;

            var result = await provider.Object.Get(
                uri, null, isProxyRequest, isRetry, nonInteractive, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal("http://host/", _actualRequest.Uri);
            Assert.True(_actualRequest.IsRetry);
            Assert.True(_actualRequest.NonInteractive);
        }

        [Fact]
        public async void WhenResponseContainsAbort_ThenThrowPluginException()
        {
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var isProxyRequest = false;
            var isRetry = true;
            var nonInteractive = true;
            _mockProvider.Setup(x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()))
                .Returns(new PluginCredentialResponse() {Abort = true});

            var exception = await Record.ExceptionAsync(async () =>await provider.Object.Get(
                uri, null, isProxyRequest, isRetry, nonInteractive, CancellationToken.None));

            Assert.IsAssignableFrom<PluginException>(exception);
            Assert.Contains(@"Credential plugin c:\path\plugin.exe handles this request, but is unable to provide credentials.", 
                exception.Message);
        }

        [Fact]
        public async void WhenResponseContainsAbortAndAbortMessage_ThenThrowPluginException()
        {
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var isProxyRequest = false;
            var isRetry = true;
            var nonInteractive = true;
            _mockProvider.Setup(x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()))
                .Returns(new PluginCredentialResponse() { Abort = true, AbortMessage = "Extra message."});

            var exception = await Record.ExceptionAsync(async () => await provider.Object.Get(
                uri, null, isProxyRequest, isRetry, nonInteractive, CancellationToken.None));

            Assert.IsAssignableFrom<PluginException>(exception);
            Assert.Contains(@"Credential plugin c:\path\plugin.exe handles this request, but is unable to provide credentials. Extra message.", 
                exception.Message);
        }

        [Fact]
        public async void WhenResponseContainsUsername_ReturnCredential()
        {
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var isProxyRequest = false;
            var isRetry = true;
            var nonInteractive = true;
            _mockProvider.Setup(x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()))
                .Returns(new PluginCredentialResponse() { Username = "u" });

            var result = await provider.Object.Get(
                uri, null, isProxyRequest, isRetry, nonInteractive, CancellationToken.None) as NetworkCredential;

            Assert.NotNull(result);
            Assert.Equal("u", result?.UserName);
        }

        [Fact]
        public async void WhenResponseContainsPassword_ReturnCredential()
        {
            var provider = _mockProvider;
            var uri = new Uri("http://host/");
            var isProxyRequest = false;
            var isRetry = true;
            var nonInteractive = true;
            _mockProvider.Setup(x => x.Execute(It.IsAny<PluginCredentialRequest>(), It.IsAny<CancellationToken>()))
                .Returns(new PluginCredentialResponse() { Password = "p" });

            var result = await provider.Object.Get(
                uri, null, isProxyRequest, isRetry, nonInteractive, CancellationToken.None) as NetworkCredential;

            Assert.NotNull(result);
            Assert.Equal("p", result?.Password);
        }
    }
}
