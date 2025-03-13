// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

# nullable enable

using System;
using System.Collections.Generic;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;
using Moq;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class KnownOwnersCapabilityTests
    {
        [Fact]
        public void Constructor_WithNullOwnerDetailsUriService_KnownOwnersIsNull()
        {
            // Arrange
            var ownersList = new List<string> { "Owner1", "Owner2" };
            IOwnerDetailsUriService? ownerDetailsUriService = null;

            // Act
            var capability = new KnownOwnersCapability(ownersList, ownerDetailsUriService);

            // Assert
            Assert.Null(capability.KnownOwners);
        }

        [Fact]
        public void Constructor_WithOwnerDetailsUriServiceNotSupportingKnownOwners_KnownOwnersIsNull()
        {
            // Arrange
            var ownersList = new List<string> { "Owner1", "Owner2" };
            var ownerDetailsUriServiceMock = new Mock<IOwnerDetailsUriService>();
            ownerDetailsUriServiceMock.Setup(s => s.SupportsKnownOwners).Returns(false);

            // Act
            var capability = new KnownOwnersCapability(ownersList, ownerDetailsUriServiceMock.Object);

            // Assert
            Assert.Null(capability.KnownOwners);
        }

        [Fact]
        public void Constructor_WithEmptyOwnersList_KnownOwnersIsEmpty()
        {
            // Arrange
            var ownersList = new List<string>();
            var ownerDetailsUriServiceMock = new Mock<IOwnerDetailsUriService>();
            ownerDetailsUriServiceMock.Setup(s => s.SupportsKnownOwners).Returns(true);

            // Act
            var capability = new KnownOwnersCapability(ownersList, ownerDetailsUriServiceMock.Object);

            // Assert
            Assert.NotNull(capability.KnownOwners);
            Assert.Empty(capability.KnownOwners);
        }

        [Fact]
        public void Constructor_WithValidOwnersList_KnownOwnersIsInitialized()
        {
            // Arrange
            var ownersList = new List<string> { "Owner1", "Owner2" };
            var ownerDetailsUriServiceMock = new Mock<IOwnerDetailsUriService>();
            ownerDetailsUriServiceMock.Setup(s => s.SupportsKnownOwners).Returns(true);
            ownerDetailsUriServiceMock.Setup(s => s.GetOwnerDetailsUri(It.IsAny<string>())).Returns((string owner) => new Uri($"http://test.com/{owner}"));

            // Act
            var capability = new KnownOwnersCapability(ownersList, ownerDetailsUriServiceMock.Object);

            // Assert
            Assert.NotNull(capability.KnownOwners);
            Assert.Equal(2, capability.KnownOwners.Count);
            Assert.Equal("Owner1", capability.KnownOwners[0].Name);
            Assert.Equal(new Uri("http://test.com/Owner1"), capability.KnownOwners[0].Link);
            Assert.Equal("Owner2", capability.KnownOwners[1].Name);
            Assert.Equal(new Uri("http://test.com/Owner2"), capability.KnownOwners[1].Link);
        }
    }
}
