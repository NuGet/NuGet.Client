// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Moq;
using NuGet.PackageManagement.UI.ViewModels;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.ViewModels
{
    [Collection(MockedVS.Collection)]
    public class ReadmePreviewViewModelTests
    {
        [Fact]
        public void Constructor_WithNullPackageFileService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new ReadmePreviewViewModel(null);
            });
        }

        [Fact]
        public void Constructor_Defaults()
        {
            //Arrange
            var mockFileService = new Mock<INuGetPackageFileService>();
            var mockServiceBroker = new Mock<IServiceBroker>();
#pragma warning disable ISB001 // Dispose of proxies
            mockServiceBroker.Setup(x => x.GetProxyAsync<INuGetPackageFileService>(It.IsAny<ServiceRpcDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockFileService.Object);
#pragma warning restore ISB001 // Dispose of proxies
            //Act
            var target = new ReadmePreviewViewModel(mockServiceBroker.Object);

            //Assert
            Assert.False(target.ErrorLoadingReadme);
            Assert.Equal(string.Empty, target.ReadmeMarkdown);
            Assert.True(target.CanDetermineReadmeDefined);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task LoadReadmeAsync_WithoutReadmeUrl_NoReadmeReturned(string readmeUrl)
        {
            //Arrange
            var mockFileService = new Mock<INuGetPackageFileService>();
            var mockServiceBroker = new Mock<IServiceBroker>();
#pragma warning disable ISB001 // Dispose of proxies
            mockServiceBroker.Setup(x => x.GetProxyAsync<INuGetPackageFileService>(It.IsAny<ServiceRpcDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockFileService.Object);
#pragma warning restore ISB001 // Dispose of proxies
            var target = new ReadmePreviewViewModel(mockServiceBroker.Object);

            //Act
            await target.LoadReadmeAsync(readmeUrl, CancellationToken.None);

            //Assert
            Assert.False(target.ErrorLoadingReadme);
            Assert.Equal(string.Empty, target.ReadmeMarkdown);
            Assert.False(target.CanDetermineReadmeDefined);
        }

        [Fact]
        public async Task LoadReadmeAsync_WithReadmeUrl_ReadmeReturned()
        {
            //Arrange
            var readmeContents = "readme contents";
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(readmeContents));
            var mockFileService = new Mock<INuGetPackageFileService>();
            mockFileService.Setup(x => x.GetReadmeAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(stream);
            var mockServiceBroker = new Mock<IServiceBroker>();
#pragma warning disable ISB001 // Dispose of proxies
            mockServiceBroker.Setup(x => x.GetProxyAsync<INuGetPackageFileService>(It.IsAny<ServiceRpcDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockFileService.Object);
#pragma warning restore ISB001 // Dispose of proxies
            var target = new ReadmePreviewViewModel(mockServiceBroker.Object);

            //Act
            await target.LoadReadmeAsync("C://path/to/readme.md", CancellationToken.None);

            //Assert
            Assert.False(target.ErrorLoadingReadme);
            Assert.Equal(readmeContents, target.ReadmeMarkdown);
            Assert.True(target.CanDetermineReadmeDefined);
        }
    }
}
