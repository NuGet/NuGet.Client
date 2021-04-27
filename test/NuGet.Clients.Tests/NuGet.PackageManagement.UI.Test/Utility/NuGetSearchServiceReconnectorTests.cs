// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.PackageManagement.UI.Utility;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Utility
{
    public class NuGetSearchServiceReconnectorTests
    {
        private readonly JoinableTaskFactory _jtf;

        public NuGetSearchServiceReconnectorTests()
        {
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            var jtc = new JoinableTaskContext();
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
            _jtf = new JoinableTaskFactory(jtc);
        }

        [Fact]
        public async Task CreateAsync_NullArguments_ThrowsArgumentNullException()
        {
            // Arrange
            var serviceBroker = new Mock<IServiceBroker>();

            // Act & Assert
            ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => NuGetSearchServiceReconnector.CreateAsync(serviceBroker: null, _jtf, CancellationToken.None));
            Assert.Equal("serviceBroker", exception.ParamName);

            exception = await Assert.ThrowsAsync<ArgumentNullException>(() => NuGetSearchServiceReconnector.CreateAsync(serviceBroker.Object, jtf: null, CancellationToken.None));
            Assert.Equal("jtf", exception.ParamName);
        }

        [Fact]
        public async Task Object_NormalCreation_GetInstanceFromServiceBroker()
        {
            // Arrange
            var searchService = new Mock<INuGetSearchService>();

            var serviceBroker = new Mock<IServiceBroker>();
            serviceBroker.Setup(sb =>
#pragma warning disable ISB001 // Dispose of proxies
            sb.GetProxyAsync<INuGetSearchService>(It.IsAny<ServiceJsonRpcDescriptor>(), It.IsAny<ServiceActivationOptions>(), It.IsAny<CancellationToken>())
#pragma warning restore ISB001 // Dispose of proxies
            )
                .ReturnsAsync(searchService.Object);

            var target = await NuGetSearchServiceReconnector.CreateAsync(serviceBroker.Object, _jtf, CancellationToken.None);

            var cancellationToken = new CancellationToken();

            // Act
            await target.Object.ContinueSearchAsync(cancellationToken);

            // Assert
            searchService.Verify(s => s.ContinueSearchAsync(cancellationToken), Times.Once);
        }

        [Fact]
        public async Task AvailabilityChangedAsync_OnChange_GetsNewInstanceFromServiceBroker()
        {
            // Arrange
            var searchService1 = new Mock<INuGetSearchService>();
            var searchService2 = new Mock<INuGetSearchService>();

            int getProxyCount = 0;
            var serviceBroker = new Mock<IServiceBroker>();
            serviceBroker.Setup(sb =>
#pragma warning disable ISB001 // Dispose of proxies
            sb.GetProxyAsync<INuGetSearchService>(It.IsAny<ServiceJsonRpcDescriptor>(), It.IsAny<ServiceActivationOptions>(), It.IsAny<CancellationToken>())
#pragma warning restore ISB001 // Dispose of proxies
            )
                .ReturnsAsync(() =>
                {
                    var count = Interlocked.Increment(ref getProxyCount);
                    switch (count)
                    {
                        case 1:
                            return searchService1.Object;

                        case 2:
                            return searchService2.Object;

                        default:
                            throw new InvalidOperationException();
                    }
                });

            // Act
            var target = await NuGetSearchServiceReconnector.CreateAsync(serviceBroker.Object, _jtf, CancellationToken.None);
            await target.AvailabilityChangedAsync();
            await target.Object.ContinueSearchAsync(CancellationToken.None);

            // Assert
            searchService2.Verify(s => s.ContinueSearchAsync(It.IsAny<CancellationToken>()), Times.Once);
            searchService2.Verify(s => s.Dispose(), Times.Never);

            searchService1.Verify(s => s.ContinueSearchAsync(It.IsAny<CancellationToken>()), Times.Never);
            searchService1.Verify(s => s.Dispose(), Times.Once);
        }

        [Fact]
        public async Task Dispose_NormalInstance_DisposesInnerService()
        {
            // Arrange
            var searchService = new Mock<INuGetSearchService>();

            var serviceBroker = new Mock<IServiceBroker>();
            serviceBroker.Setup(sb =>
#pragma warning disable ISB001 // Dispose of proxies
            sb.GetProxyAsync<INuGetSearchService>(It.IsAny<ServiceJsonRpcDescriptor>(), It.IsAny<ServiceActivationOptions>(), It.IsAny<CancellationToken>())
#pragma warning restore ISB001 // Dispose of proxies
            )
                .ReturnsAsync(searchService.Object);

            var target = await NuGetSearchServiceReconnector.CreateAsync(serviceBroker.Object, _jtf, CancellationToken.None);

            // Act
            target.Dispose();

            // Assert
            searchService.Verify(s => s.Dispose(), Times.Once);
        }

        [Fact]
        public async Task Dispose_DisposeInnerService_DoesNotDisposedBrokeredService()
        {
            // Arrange
            var searchService = new Mock<INuGetSearchService>();

            var serviceBroker = new Mock<IServiceBroker>();
            serviceBroker.Setup(sb =>
#pragma warning disable ISB001 // Dispose of proxies
            sb.GetProxyAsync<INuGetSearchService>(It.IsAny<ServiceJsonRpcDescriptor>(), It.IsAny<ServiceActivationOptions>(), It.IsAny<CancellationToken>())
#pragma warning restore ISB001 // Dispose of proxies
            )
                .ReturnsAsync(searchService.Object);

            var target = await NuGetSearchServiceReconnector.CreateAsync(serviceBroker.Object, _jtf, CancellationToken.None);

            // Act
            target.Object.Dispose();

            // Assert
            searchService.Verify(s => s.Dispose(), Times.Never);
        }
    }
}
