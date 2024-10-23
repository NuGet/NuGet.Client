// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                new ReadmePreviewViewModel(null, ItemFilter.All, true);
            });
        }

        [Fact]
        public void Constructor_Defaults()
        {
            //Arrange
            var mockFileService = new Mock<INuGetPackageFileService>();

            //Act
            var target = new ReadmePreviewViewModel(mockFileService.Object, ItemFilter.All, true);

            //Assert
            Assert.False(target.ErrorLoadingReadme);
            Assert.Equal(string.Empty, target.ReadmeMarkdown);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task SetPackageMetadataAsync_WithoutReadmeUrl_NoReadmeReturned(string readmeUrl)
        {
            //Arrange
            var mockFileService = new Mock<INuGetPackageFileService>();
            var target = new ReadmePreviewViewModel(mockFileService.Object, ItemFilter.Installed, true);
            var package = new DetailedPackageMetadata();
            package.ReadmeFileUrl = readmeUrl;

            //Act
            await target.SetPackageMetadataAsync(package, CancellationToken.None);

            //Assert
            Assert.False(target.ErrorLoadingReadme);
            Assert.Equal(string.Empty, target.ReadmeMarkdown);
        }

        [Fact]
        public async Task SetCurrentFilter_ItemFilterAll_NoLocalReadmeReturned()
        {
            //Arrange
            var readmeContents = "readme contents";
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(readmeContents));
            var mockFileService = new Mock<INuGetPackageFileService>();
            mockFileService.Setup(x => x.GetReadmeAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(stream);
            var target = new ReadmePreviewViewModel(mockFileService.Object, ItemFilter.Installed, true);
            var package = new DetailedPackageMetadata();
            package.ReadmeFileUrl = "C://path/to/readme.md";
            await target.SetPackageMetadataAsync(package, CancellationToken.None);

            //Act
            await target.ItemFilterChangedAsync(ItemFilter.All);

            //Assert
            Assert.False(target.ErrorLoadingReadme);
            Assert.Equal(string.Empty, target.ReadmeMarkdown);
        }

        [Theory]
        [InlineData(ItemFilter.Installed)]
        [InlineData(ItemFilter.Consolidate)]
        [InlineData(ItemFilter.UpdatesAvailable)]
        public async Task SetCurrentFilter_ItemFilterRenderingLocalReadme_LocalReadmeReturned(ItemFilter filter)
        {
            //Arrange
            var readmeContents = "readme contents";
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(readmeContents));
            var mockFileService = new Mock<INuGetPackageFileService>();
            mockFileService.Setup(x => x.GetReadmeAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(stream);
            var target = new ReadmePreviewViewModel(mockFileService.Object, ItemFilter.All, true);
            var package = new DetailedPackageMetadata();
            package.ReadmeFileUrl = "C://path/to/readme.md";
            await target.SetPackageMetadataAsync(package, CancellationToken.None);

            //Act
            await target.ItemFilterChangedAsync(filter);

            //Assert
            Assert.False(target.ErrorLoadingReadme);
            Assert.Equal(readmeContents, target.ReadmeMarkdown);
        }

        [Fact]
        public async Task SetPackageMetadataAsync_WithLocalReadmeUrl_RenderLocalReadmeTrue_LocalReadmeReturned()
        {
            //Arrange
            var readmeContents = "readme contents";
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(readmeContents));
            var mockFileService = new Mock<INuGetPackageFileService>();
            mockFileService.Setup(x => x.GetReadmeAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(stream);
            var target = new ReadmePreviewViewModel(mockFileService.Object, ItemFilter.Installed, true);
            var package = new DetailedPackageMetadata();
            package.ReadmeFileUrl = "C://path/to/readme.md";

            //Act
            await target.SetPackageMetadataAsync(package, CancellationToken.None);

            //Assert
            Assert.False(target.ErrorLoadingReadme);
            Assert.Equal(readmeContents, target.ReadmeMarkdown);
        }

        [Fact]
        public async Task SetPackageMetadataAsync_WithLocalReadmeUrl_RenderLocalReadmeFalse_NoLocalReadmeReturned()
        {
            //Arrange
            var readmeContents = "readme contents";
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(readmeContents));
            var mockFileService = new Mock<INuGetPackageFileService>();
            mockFileService.Setup(x => x.GetReadmeAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(stream);
            var target = new ReadmePreviewViewModel(mockFileService.Object, ItemFilter.All, true);
            var package = new DetailedPackageMetadata();
            package.ReadmeFileUrl = "C://path/to/readme.md";

            //Act
            await target.SetPackageMetadataAsync(package, CancellationToken.None);

            //Assert
            Assert.False(target.ErrorLoadingReadme);
            Assert.Equal(string.Empty, target.ReadmeMarkdown);
        }

        [Fact]
        public async Task SetPackageMetadataAsync_WithLocalReadmeUrl_FileNotFound_NoReadmeFoundTextReturned()
        {
            //Arrange
            var mockFileService = new Mock<INuGetPackageFileService>();
            mockFileService.Setup(x => x.GetReadmeAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).Returns(null);
            var target = new ReadmePreviewViewModel(mockFileService.Object, ItemFilter.Installed, true);
            var package = new DetailedPackageMetadata();
            package.ReadmeFileUrl = "C://path/to/readme.md";

            //Act
            await target.SetPackageMetadataAsync(package, CancellationToken.None);

            //Assert
            Assert.False(target.ErrorLoadingReadme);
            Assert.Equal(Resources.Text_NoReadme, target.ReadmeMarkdown);
        }
    }
}
