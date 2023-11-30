// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class PackageSearchResultJsonPrinterTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Add_MultiplePackageSearchMetadata_RendersCorrectNumberOfPackages(int numberOfPackages)
        {
            // Arrange
            var mockLoggerWithColor = new Mock<ILoggerWithColor>();
            var printer = new PackageSearchResultJsonPrinter(mockLoggerWithColor.Object, PackageSearchVerbosity.Normal);
            var mockSource = new Mock<PackageSource>("http://mocksource", "MockSource");
            var completedSearch = new List<IPackageSearchMetadata>();
            var mockMetadata = new Mock<IPackageSearchMetadata>();
            var packageIdentity = new PackageIdentity("NuGet.Versioning", new NuGetVersion("4.3.0"));

            mockMetadata.Setup(m => m.Identity).Returns(packageIdentity);
            mockMetadata.Setup(m => m.Authors).Returns("Microsoft");
            mockMetadata.Setup(m => m.DownloadCount).Returns(123456);
            printer.Start();

            for (int i = 0; i < numberOfPackages; i++)
            {
                completedSearch.Add(mockMetadata.Object);
            }

            // Act
            printer.Add(mockSource.Object, completedSearch);
            printer.Finish();

            // Assert
            var expectedZeroJson = $@"[
  {{
    ""sourceName"": ""MockSource"",
    ""packages"": []
  }}
]";

            var expectedOneJson = $@"[
  {{
    ""sourceName"": ""MockSource"",
    ""packages"": [
      {{
        ""total downloads"": 123456,
        ""owners"": null,
        ""id"": ""NuGet.Versioning"",
        ""latestVersion"": ""4.3.0""
      }}
    ]
  }}
]";

            var expectedTwoJson = $@"[
  {{
    ""sourceName"": ""MockSource"",
    ""packages"": [
      {{
        ""total downloads"": 123456,
        ""owners"": null,
        ""id"": ""NuGet.Versioning"",
        ""latestVersion"": ""4.3.0""
      }},
      {{
        ""total downloads"": 123456,
        ""owners"": null,
        ""id"": ""NuGet.Versioning"",
        ""latestVersion"": ""4.3.0""
      }}
    ]
  }}
]";

            if (numberOfPackages == 2)
            {
                mockLoggerWithColor.Verify(x => x.LogMinimal(expectedTwoJson), Times.Exactly(1));
            }
            else if (numberOfPackages == 0)
            {
                mockLoggerWithColor.Verify(x => x.LogMinimal(expectedZeroJson), Times.Exactly(1));
            }
            else
            {
                mockLoggerWithColor.Verify(x => x.LogMinimal(expectedOneJson), Times.Exactly(1));
            }
        }

        [Fact]
        public void Add_Error_ShouldLogError()
        {
            // Arrange
            var mockLoggerWithColor = new Mock<ILoggerWithColor>();
            var printer = new PackageSearchResultJsonPrinter(mockLoggerWithColor.Object, PackageSearchVerbosity.Minimal);
            Mock<PackageSource> mockSource = new Mock<PackageSource>("http://errorsource", "ErrorTestSource");
            string errorMessage = "An error occurred";

            // Act
            printer.Add(mockSource.Object, errorMessage);

            // Assert
            mockLoggerWithColor.Verify(x => x.LogMinimal("****************************************"), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogMinimal($"Source: ErrorTestSource (http://errorsource/)"), Times.Once);
            mockLoggerWithColor.Verify(x => x.LogError(errorMessage), Times.Once);
        }
    }
}
