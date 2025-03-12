// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.PackageManagement.UI.Models;
using Xunit;
using NuGet.Versioning;
using NuGet.Packaging;
using Moq;

namespace NuGet.PackageManagement.UI.Test
{
    public class InstalledPackageModelTests
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
            var title = "Test Package";
            var description = "Test Description";
            var authors = "Test Author";
            var projectUrl = new Uri("http://test.com");
            var tags = new[] { "tag1", "tag2" };
            var copyright = "Test Copyright";
            var ownersList = new List<string> { "Owner1", "Owner2" };
            var packageDependencyGroups = new List<PackageDependencyGroup>();
            var summary = "Test Summary";
            var published = DateTimeOffset.Now;

            // Act
            var model = new InstalledPackageModel(
                identity,
                packagePath,
                vulnerabilityCapability.Object,
                title,
                description,
                authors,
                projectUrl,
                tags,
                copyright,
                ownersList,
                packageDependencyGroups,
                summary,
                published,
                reportAbuseUrl);

            // Assert
            Assert.Equal(reportAbuseUrl, model.ReportAbuseUrl);
        }
    }
}
