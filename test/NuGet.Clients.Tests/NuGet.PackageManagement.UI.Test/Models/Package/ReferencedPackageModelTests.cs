// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;
using NuGet.PackageManagement.UI.Models;
using Xunit;
using NuGet.Versioning;
using Moq;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class ReferencedPackageModelTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("String")]
        public void Constructor_SetReportAbuseUrl_InitializeReportAbuseUrl(string reportAbuseUrl)
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packagePath = "path/to/package";
            var vulnerabilityCapability = new Mock<IVulnerable>();
            var mockEmbeddedResource = new Mock<IEmbeddedResources>();

            // Act
            var model = new ReferencedPackageModel(
                identity,
                packagePath,
                vulnerabilityCapability.Object,
                mockEmbeddedResource.Object,
                reportAbuseUrl: reportAbuseUrl);

            // Assert
            Assert.Equal(reportAbuseUrl, model.ReportAbuseUrl);
        }
    }
}
