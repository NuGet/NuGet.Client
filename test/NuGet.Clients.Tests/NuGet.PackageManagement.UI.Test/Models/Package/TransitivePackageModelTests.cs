// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.PackageManagement.UI.Models;
using Xunit;
using Moq;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class TransitivePackageModelTests
    {
        [Fact]
        public void Constructor_InitializesTransitiveOrigins()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packagePath = "path/to/package";
            var vulnerabilityCapability = new Mock<IVulnerable>();
            var transitiveOrigins = new List<PackageIdentity>
            {
                new PackageIdentity("OriginPackage1", new NuGetVersion("1.0.0")),
                new PackageIdentity("OriginPackage2", new NuGetVersion("2.0.0"))
            };

            // Act
            var model = new TransitivePackageModel(
                identity,
                packagePath,
                vulnerabilityCapability.Object,
                transitiveOrigins);

            // Assert
            Assert.Equal(transitiveOrigins, model.TransitiveOrigins);
        }
    }
}
