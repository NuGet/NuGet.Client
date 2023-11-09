// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class PackageSearchResultTableRendererTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Add_OnePackageSearchMetadata_RendersOnePackageTable(bool exactMatch)
        {
            // Arrange
            var searchTerm = "TestPackage";
            Mock<ILoggerWithColor> mockLoggerWithColor = new Mock<ILoggerWithColor>();
            PackageSearchResultTableRenderer renderer = new PackageSearchResultTableRenderer(searchTerm, mockLoggerWithColor.Object, exactMatch);
            Mock<PackageSource> mockSource = new Mock<PackageSource>("http://mysource", "TestSource");
            PackageIdentity packageIdentity = new PackageIdentity("NuGet.Versioning", new NuGetVersion("4.3.0"));
            Mock<IPackageSearchMetadata> packageSearchMetadata = new Mock<IPackageSearchMetadata>();
            packageSearchMetadata.Setup(m => m.Identity).Returns(packageIdentity);
            packageSearchMetadata.Setup(m => m.Authors).Returns("Microsoft");
            packageSearchMetadata.Setup(m => m.DownloadCount).Returns(123456);
            Task<IEnumerable<IPackageSearchMetadata>> completedSearchTask = Task.FromResult<IEnumerable<IPackageSearchMetadata>>(new List<IPackageSearchMetadata> { packageSearchMetadata.Object });

            // Act
            await renderer.Add(mockSource.Object, completedSearchTask);

            // Assert
            mockLoggerWithColor.Verify(x => x.LogMinimal("****************************************"), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("Source: TestSource (http://mysource/)"), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("| Package ID       ", System.Console.ForegroundColor), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("| Latest Version ", System.Console.ForegroundColor), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("| Authors   ", System.Console.ForegroundColor), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("| Downloads ", System.Console.ForegroundColor), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("|"), Times.Exactly(3));
            mockLoggerWithColor.Verify(x => x.LogMinimal("|------------------", System.Console.ForegroundColor), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("|----------------", System.Console.ForegroundColor), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("|-----------", System.Console.ForegroundColor), Times.Exactly(2));
            mockLoggerWithColor.Verify(x => x.LogMinimal("| NuGet.Versioning ", System.Console.ForegroundColor), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("| 4.3.0          ", System.Console.ForegroundColor), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("| Microsoft ", System.Console.ForegroundColor), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("| 123,456   ", System.Console.ForegroundColor), Times.Once);
        }

        [Fact]
        public async Task Add_NulSearchTask_LogSourceNotFound()
        {
            // Arrange
            var searchTerm = "TestPackage";
            Mock<ILoggerWithColor> mockLoggerWithColor = new Mock<ILoggerWithColor>();
            PackageSearchResultTableRenderer renderer = new PackageSearchResultTableRenderer(searchTerm, mockLoggerWithColor.Object, false);

            Mock<PackageSource> mockSource = new Mock<PackageSource>("http://mysource", "TestSource");

            Task<IEnumerable<IPackageSearchMetadata>> completedSearchTask = null;

            // Act
            await renderer.Add(mockSource.Object, completedSearchTask);

            // Assert
            mockLoggerWithColor.Verify(x => x.LogMinimal("****************************************"), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("Source: TestSource (http://mysource/)"), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal("Failed to obtain a search resource."), Times.Once);
        }

        [Fact]
        public async Task Add_SearchFatalProtocolException_CatchesExceptionByLoggingError()
        {
            // Arrange
            var searchTerm = "FaultyPackage";
            Mock<ILoggerWithColor> mockLoggerWithColor = new Mock<ILoggerWithColor>();
            PackageSearchResultTableRenderer renderer = new PackageSearchResultTableRenderer(searchTerm, mockLoggerWithColor.Object, false);

            Mock<PackageSource> mockSource = new Mock<PackageSource>("http://badsource.com", "BadSource");
            Task<IEnumerable<IPackageSearchMetadata>> faultyTask = Task<IEnumerable<IPackageSearchMetadata>>.Factory.StartNew(() => throw new FatalProtocolException("Protocol error"));

            // Act
            var exception = await Record.ExceptionAsync(() => renderer.Add(mockSource.Object, faultyTask));

            // Assert
            Assert.Null(exception);
            mockLoggerWithColor.Verify(x => x.LogError("Protocol error"), Times.Once);
        }

        [Fact]
        public async Task Add_SearchOperationCanceledException_CatchesExceptionByLoggingError()
        {
            // Arrange
            var searchTerm = "FaultyPackage";
            Mock<ILoggerWithColor> mockLoggerWithColor = new Mock<ILoggerWithColor>();
            PackageSearchResultTableRenderer renderer = new PackageSearchResultTableRenderer(searchTerm, mockLoggerWithColor.Object, false);

            Mock<PackageSource> mockSource = new Mock<PackageSource>("http://badsource.com", "BadSource");
            Task<IEnumerable<IPackageSearchMetadata>> faultyTask = Task<IEnumerable<IPackageSearchMetadata>>.Factory.StartNew(() => throw new OperationCanceledException("Operation canceled error"));

            // Act
            var exception = await Record.ExceptionAsync(() => renderer.Add(mockSource.Object, faultyTask));

            // Assert
            Assert.Null(exception);
            mockLoggerWithColor.Verify(x => x.LogError("Operation canceled error"), Times.Once);
        }

        [Fact]
        public async Task Add_SearchInvalidOperationException_CatchesExceptionByLoggingError()
        {
            // Arrange
            var searchTerm = "FaultyPackage";
            Mock<ILoggerWithColor> mockLoggerWithColor = new Mock<ILoggerWithColor>();
            PackageSearchResultTableRenderer renderer = new PackageSearchResultTableRenderer(searchTerm, mockLoggerWithColor.Object, false);

            Mock<PackageSource> mockSource = new Mock<PackageSource>("c:/path", "BadSource");
            Task<IEnumerable<IPackageSearchMetadata>> faultyTask = Task<IEnumerable<IPackageSearchMetadata>>.Factory.StartNew(() => throw new InvalidOperationException("Invalid folder error"));

            // Act
            var exception = await Record.ExceptionAsync(() => renderer.Add(mockSource.Object, faultyTask));

            // Assert
            Assert.Null(exception);
            mockLoggerWithColor.Verify(x => x.LogError("Invalid folder error"), Times.Once);
        }
    }
}
