// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Credentials.Test
{
    
    public class CredentialServiceTests
    {
        private readonly StringBuilder _testErrorOutput = new StringBuilder();

        private readonly Mock<ICredentialProvider> _mockProvider;

        public CredentialServiceTests()
        {
            _mockProvider = new Mock<ICredentialProvider>();
            _mockProvider.Setup(x => x
                .Get(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<bool>(), It.IsAny<bool>(),
                    It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<ICredentials>(null));
        }

        private void TestableErrorWriter(string s)
        {
            _testErrorOutput.AppendLine(s);
        }

        [Fact]
        public async Task GetCredentials_PassesAllParametersToProviders()
        {
            var service = new CredentialService(errorDelegate: TestableErrorWriter, nonInteractive: true,
                useCache:true);
            var webProxy = new WebProxy();
            var uri = new Uri("http://uri");
            service.Providers = new[] { _mockProvider.Object };

            await service.GetCredentials(uri, webProxy, isProxy: true, cancellationToken: CancellationToken.None);

            _mockProvider.Verify(x => x.Get(uri, webProxy, /*isProxy*/ true, /*isRetry*/ It.IsAny<bool>(),
                /*nonInteractive*/ true, CancellationToken.None));
        }

        [Fact]
        public async Task GetCredentials_FirstCallHasRetryFalse()
        {
            var service = new CredentialService(errorDelegate: TestableErrorWriter, nonInteractive: false,
                useCache: true)
            {
                Providers = new[] {_mockProvider.Object}
            };
            _mockProvider.Setup(x => x.Get(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<ICredentials>(new NetworkCredential()));
            var uri1 = new Uri("http://uri1");
            var uri2 = new Uri("http://uri2");

            var result1 = await service.GetCredentials(uri1, null, isProxy: false,
                cancellationToken: CancellationToken.None);
            var result2 = await service.GetCredentials(uri2, null, isProxy: false,
                cancellationToken: CancellationToken.None);

            _mockProvider.Verify(x => x.Get(uri1, null, /*isProxy*/ false, /*isRetry*/ false,
                /*nonInteractive*/ false, CancellationToken.None));
            _mockProvider.Verify(x => x.Get(uri2, null, /*isProxy*/ false, /*isRetry*/ false,
                /*nonInteractive*/ false, CancellationToken.None));
        }

        [Fact]
        public async Task GetCredentials_SecondCallHasRetryTrue()
        {
            var service = new CredentialService(errorDelegate: TestableErrorWriter, nonInteractive: false,
                useCache: true)
            {
                Providers = new[] {_mockProvider.Object}
            };
            _mockProvider.Setup(x => x.Get(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<ICredentials>(new NetworkCredential()));
            var uri1 = new Uri("http://uri1");
            var webProxy = new WebProxy();

            await service.GetCredentials(uri1, null, isProxy: false,
                cancellationToken: CancellationToken.None);
            await service.GetCredentials(uri1, webProxy, isProxy: false,
                cancellationToken: CancellationToken.None);

            _mockProvider.Verify(x => x.Get(uri1, null, /*isProxy*/ false, /*isRetry*/ false,
                /*nonInteractive*/ false, CancellationToken.None));
            _mockProvider.Verify(x => x.Get(uri1, webProxy, /*isProxy*/ false, /*isRetry*/ true,
                /*nonInteractive*/ false, CancellationToken.None));
        }

        [Fact]
        public void GetCredentials_UsesDefaultProviders()
        {
            var origDefaultProvider = CredentialService.DefaultProviders;
            try
            {
                var providers = new[] {_mockProvider.Object};
                CredentialService.DefaultProviders = providers;

                var service = new CredentialService(errorDelegate: TestableErrorWriter, nonInteractive: false,
                    useCache: true);

                Assert.Equal(1, service.Providers.Count());
                Assert.Same(_mockProvider.Object, service.Providers.First());
            }
            finally
            {
                CredentialService.DefaultProviders = origDefaultProvider;
            }
        }

        private static int _lockTestConcurrencyCount = 0;
        [Fact]
        public void GetCredentials_SingleThreadedAccessToEachProvider()
        {
            // Arrange
            var service = new CredentialService(errorDelegate: TestableErrorWriter, nonInteractive: true,
                useCache: true);
            var webProxy = new WebProxy();
            var uri = new Uri("http://uri");
            service.Providers = new[] { _mockProvider.Object };
            _mockProvider
                .Setup(x => x.Get(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    _lockTestConcurrencyCount++;
                    Assert.Equal(1, _lockTestConcurrencyCount);
                    _lockTestConcurrencyCount--;
                    return Task.FromResult<ICredentials>(new NetworkCredential());
                });
            var tasks = new Task[10];

            // Act
            for (var x = 0; x < 10; x++)
            {
                tasks[x]=service.GetCredentials(uri, webProxy, isProxy: false,
                    cancellationToken: CancellationToken.None);
            }
            Task.WaitAll(tasks);

            // Assert
            // in this case, assert is done during provider access
        }

        [Fact]
        public async Task GetCredentials_WhenUriHasSameAuthority_ThenReturnsCachedCredential()
        {
            var service = new CredentialService(errorDelegate: TestableErrorWriter, nonInteractive: false,
                useCache: true)
            {
                Providers = new[] { _mockProvider.Object }
            };
            _mockProvider.Setup(x => x.Get(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ICredentials>(new NetworkCredential()));
            var uri1 = new Uri("http://host/some/path");
            var uri2 = new Uri("http://host/some2/path2");


            var result1 = await service.GetCredentials(uri1, null, isProxy: false,
                cancellationToken: CancellationToken.None);
            var result2 = await service.GetCredentials(uri2, null, isProxy: false,
                cancellationToken: CancellationToken.None);

            Assert.Same(result1, result2);
            _mockProvider.Verify(
                x => x.Get(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetCredentials_WhenUseCacheFalse_ThenDoNotReturnCachedCredentials()
        {
            var service = new CredentialService(errorDelegate: TestableErrorWriter, nonInteractive: false,
                useCache: false)
            {
                Providers = new[] { _mockProvider.Object }
            };
            _mockProvider.Setup(x => x.Get(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(()=>Task.FromResult<ICredentials>(new NetworkCredential()));
            var uri1 = new Uri("http://host/some/path");
            var uri2 = new Uri("http://host/some2/path2");


            var result1 = await service.GetCredentials(uri1, null, isProxy: false,
                cancellationToken: CancellationToken.None);
            var result2 = await service.GetCredentials(uri2, null, isProxy: false,
                cancellationToken: CancellationToken.None);

            Assert.NotSame(result1, result2);
            _mockProvider.Verify(
                x => x.Get(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GetCredentials_WhenRetry_ThenDoesNotReturnCachedCredential()
        {
            var service = new CredentialService(errorDelegate: TestableErrorWriter, nonInteractive: false,
                useCache: true)
            {
                Providers = new[] { _mockProvider.Object }
            };
            _mockProvider
                .Setup(x => x.Get(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<ICredentials>(new NetworkCredential()));
            var uri1 = new Uri("http://uri1");

            var result1 = await service.GetCredentials(uri1, null, isProxy: false,
                cancellationToken: CancellationToken.None);
            var result2 = await service.GetCredentials(uri1, null, isProxy: false,
                cancellationToken: CancellationToken.None);

            Assert.NotSame(result1, result2);
            _mockProvider.Verify(
                x => x.Get(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }
    }
}
