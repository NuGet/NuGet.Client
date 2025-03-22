// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Model;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class NoDeprecationCapabilityTests
    {
        [Fact]
        public void IsDeprecated_UnderAnyCondition_ReturnsFalse()
        {
            // Arrange
            var capability = new NoDeprecationCapability();

            // Act
            var result = capability.IsDeprecated;

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void PackageDeprecationReasons_UnderAnyCondition_ReturnsUnknown()
        {
            // Arrange
            var capability = new NoDeprecationCapability();

            // Act
            var result = capability.PackageDeprecationReasons;

            // Assert
            Assert.Equal(PackageDeprecationReasonEnum.Unknown, result);
        }

        [Fact]
        public void AlternatePackage_UnderAnyCondition_ReturnsNull()
        {
            // Arrange
            var capability = new NoDeprecationCapability();

            // Act
            var result = capability.AlternatePackage;

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task PopulateDataAsync_UnderAnyCondition_ReturnsCompleteTask()
        {
            // Arrange
            var capability = new NoDeprecationCapability();
            var cancellationToken = CancellationToken.None;

            // Act
            var task = capability.PopulateDataAsync(cancellationToken);
            await task;

            // Assert
            Assert.True(task.IsCompleted);
        }
    }
}
