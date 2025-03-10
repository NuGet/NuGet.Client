// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class PackageModelTests
    {
        [Fact]
        public void PackageModelCtr_IdAndVersion_ReturnsValueFromIdentity()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));

            // Act
            var package = new TestPackageModel(identity);

            // Assert
            Assert.Equal("TestPackage", package.Id);
            Assert.Equal(new NuGetVersion("1.0.0"), package.Version);
        }

        [Fact]
        public void PackageModelCtr_NullIdentity_ThrowsArgumentNullException()
        {
            // Arrange
            PackageIdentity? identity = null;

            // Act
            // Assert
            Assert.Throws<ArgumentNullException>("identity", () => new TestPackageModel(identity!));
        }
    }
}
