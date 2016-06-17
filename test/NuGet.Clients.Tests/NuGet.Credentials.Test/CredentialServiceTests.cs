// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NuGet.Configuration;
using WebProxy = System.Net.WebProxy;

namespace NuGet.Credentials.Test
{
    
    public class CredentialServiceTests
    {
        private readonly StringBuilder _testErrorOutput = new StringBuilder();

        private readonly Mock<ICredentialProvider> _mockProvider;

        public CredentialServiceTests()
        {
            _mockProvider = new Mock<ICredentialProvider>();
            _mockProvider
                .Setup(x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new CredentialResponse (CredentialStatus.ProviderNotApplicable)));
            _mockProvider.Setup(x => x.Id).Returns("1");
        }

        private void TestableErrorWriter(string s)
        {
            _testErrorOutput.AppendLine(s);
        }

        [Fact]
        public async Task GetCredentials_PassesAllParametersToProviders()
        {
            // Arrange
            var service = new CredentialService(
                new[] { _mockProvider.Object },
                TestableErrorWriter,
                nonInteractive: true);
            var webProxy = new WebProxy();
            var uri = new Uri("http://uri");

            // Act
            await service.GetCredentialsAsync(
                uri,
                webProxy,
                CredentialRequestType.Proxy,
                message: "A",
                cancellationToken: CancellationToken.None);

            // Assert
            _mockProvider.Verify(x => x.GetAsync(
                uri,
                webProxy,
                /*type*/ CredentialRequestType.Proxy,
                /*message*/ "A",
                /*isRetry*/ It.IsAny<bool>(),
                /*nonInteractive*/ true,
                CancellationToken.None));
        }

        [Fact]
        public async Task GetCredentials_FirstCallHasRetryFalse()
        {
            // Arrange
            var service = new CredentialService(
                new[] { _mockProvider.Object },
                TestableErrorWriter,
                nonInteractive: false);
            _mockProvider.Setup(
                x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new CredentialResponse (new NetworkCredential(), CredentialStatus.Success)));
            var uri1 = new Uri("http://uri1");
            var uri2 = new Uri("http://uri2");

            // Act
            var result1 = await service.GetCredentialsAsync(
                uri1,
                proxy: null,
                type: CredentialRequestType.Unauthorized,
                message: "A",
                cancellationToken: CancellationToken.None);
            var result2 = await service.GetCredentialsAsync(
                uri2,
                proxy: null,
                type: CredentialRequestType.Unauthorized,
                message: "B",
                cancellationToken: CancellationToken.None);

            // Assert
            _mockProvider.Verify(x => x.GetAsync(
                uri1,
                /*webProxy*/ null,
                /*type*/ CredentialRequestType.Unauthorized,
                /*message*/ "A",
                /*isRetry*/ false,
                /*nonInteractive*/ false,
                CancellationToken.None));

            _mockProvider.Verify(x => x.GetAsync(
                uri2,
                /*webProxy*/ null,
                /*type*/ CredentialRequestType.Unauthorized,
                /*message*/ "B",
                /*isRetry*/ false,
                /*nonInteractive*/ false,
                CancellationToken.None));
        }

        [Fact]
        public async Task GetCredentials_SecondCallHasRetryTrue()
        {
            // Arrange
            var service = new CredentialService(
                new[] { _mockProvider.Object },
                TestableErrorWriter,
                nonInteractive: false);
            _mockProvider.Setup(
                x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new CredentialResponse (new NetworkCredential(), CredentialStatus.Success)));
            var uri1 = new Uri("http://uri1");
            var webProxy = new WebProxy();

            // Act
            await service.GetCredentialsAsync(
                uri1,
                proxy: null,
                type: CredentialRequestType.Unauthorized,
                message: null,
                cancellationToken: CancellationToken.None);
            await service.GetCredentialsAsync(
                uri1,
                webProxy,
                type: CredentialRequestType.Unauthorized,
                message: null,
                cancellationToken: CancellationToken.None);

            // Assert
            _mockProvider.Verify(x => x.GetAsync(
                uri1,
                null,
                /*type*/ CredentialRequestType.Unauthorized,
                /*message*/ null,
                /*isRetry*/ false,
                /*nonInteractive*/ false,
                CancellationToken.None));
            _mockProvider.Verify(x => x.GetAsync(
                uri1,
                webProxy,
                /*type*/ CredentialRequestType.Unauthorized,
                /*message*/ null,
                /*isRetry*/ true,
                /*nonInteractive*/ false,
                CancellationToken.None));
        }

        private static int _lockTestConcurrencyCount = 0;
        [Fact]
        public void GetCredentials_SingleThreadedAccessToEachProvider()
        {
            // Arrange
            var service = new CredentialService(
                new[] { _mockProvider.Object },
                TestableErrorWriter,
                nonInteractive: true);
            var webProxy = new WebProxy();
            var uri = new Uri("http://uri");
            _mockProvider
                .Setup(x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    _lockTestConcurrencyCount++;
                    Assert.Equal(1, _lockTestConcurrencyCount);
                    _lockTestConcurrencyCount--;
                    return Task.FromResult(new CredentialResponse (new NetworkCredential(), CredentialStatus.Success));
                });
            var tasks = new Task[10];

            // Act
            for (var x = 0; x < 10; x++)
            {
                tasks[x] = service.GetCredentialsAsync(
                    uri,
                    webProxy,
                    type: CredentialRequestType.Unauthorized,
                    message: null,
                    cancellationToken: CancellationToken.None);
            }
            Task.WaitAll(tasks);

            // Assert
            // in this case, assert is done during provider access
        }

        [Fact]
        public async Task GetCredentials_WhenUriHasSameAuthority_ThenReturnsCachedCredential()
        {
            // Arrange
            var service = new CredentialService(
                new[] { _mockProvider.Object },
                TestableErrorWriter,
                nonInteractive: false);
            _mockProvider
                .Setup(x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(new CredentialResponse (new NetworkCredential(), CredentialStatus.Success)));
            var uri1 = new Uri("http://host/some/path");
            var uri2 = new Uri("http://host/some2/path2");

            // Act
            var result1 = await service.GetCredentialsAsync(
                uri1,
                proxy: null,
                type: CredentialRequestType.Unauthorized,
                message: null,
                cancellationToken: CancellationToken.None);
            var result2 = await service.GetCredentialsAsync(
                uri2,
                proxy: null,
                type: CredentialRequestType.Unauthorized,
                message: null,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Same(result1, result2);
            _mockProvider.Verify(
                x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetCredentials_NullResponsesAreCached()
        {
            // Arrange
            var service = new CredentialService(
                new[] { _mockProvider.Object },
                TestableErrorWriter,
                nonInteractive: false);
            _mockProvider
                .Setup(x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(new CredentialResponse (CredentialStatus.ProviderNotApplicable)));
            var uri1 = new Uri("http://host/some/path");
            var uri2 = new Uri("http://host/some2/path2");

            // Act
            var result1 = await service.GetCredentialsAsync(
                uri1,
                proxy: null,
                type: CredentialRequestType.Unauthorized,
                message: null,
                cancellationToken: CancellationToken.None);
            var result2 = await service.GetCredentialsAsync(
                uri2,
                proxy: null,
                type: CredentialRequestType.Unauthorized,
                message: null,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Null(result1);
            Assert.Null(result2);
            _mockProvider.Verify(
                x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetCredentials_TriesAllProviders_EvenWhenSameType()
        {
            // Arrange
            var mockProvider1 = new Mock<ICredentialProvider>();
            mockProvider1
                .Setup(x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new CredentialResponse(CredentialStatus.ProviderNotApplicable)));
            mockProvider1.Setup(x => x.Id).Returns("1");
            var mockProvider2 = new Mock<ICredentialProvider>();
            mockProvider2
                .Setup(x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(
                    Task.FromResult(new CredentialResponse(CredentialStatus.ProviderNotApplicable)));
            mockProvider2.Setup(x => x.Id).Returns("2");
            var service = new CredentialService(
                new[] {mockProvider1.Object, mockProvider2.Object},
                TestableErrorWriter,
                nonInteractive: false);
            var uri1 = new Uri("http://host/some/path");

            // Act
            var result1 = await service.GetCredentialsAsync(
                uri1,
                proxy: null,
                type: CredentialRequestType.Unauthorized,
                message: null,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Null(result1);
            mockProvider1.Verify(
                x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            mockProvider2.Verify(
                x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetCredentials_WhenRetry_ThenDoesNotReturnCachedCredential()
        {
            // Arrange
            var service = new CredentialService(
                new[] { _mockProvider.Object },
                TestableErrorWriter,
                nonInteractive: false);
            _mockProvider
                .Setup(x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(new CredentialResponse(new NetworkCredential(), CredentialStatus.Success)));
            var uri1 = new Uri("http://uri1");

            // Act
            var result1 = await service.GetCredentialsAsync(
                uri1,
                proxy: null,
                type: CredentialRequestType.Unauthorized,
                message: null,
                cancellationToken: CancellationToken.None);
            var result2 = await service.GetCredentialsAsync(
                uri1,
                proxy: null,
                type: CredentialRequestType.Unauthorized,
                message: null,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotSame(result1, result2);
            _mockProvider.Verify(
                x => x.GetAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IWebProxy>(),
                    It.IsAny<CredentialRequestType>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }
    }
}
