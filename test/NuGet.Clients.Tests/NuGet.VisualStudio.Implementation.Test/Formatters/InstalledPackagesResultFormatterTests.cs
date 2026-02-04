// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using NuGet.VisualStudio.Contracts;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.Formatters
{
    public sealed class InstalledPackagesResultFormatterTests : FormatterTests
    {
        [Fact]
        public void SerializeThenDeserialize_WithSuccessfulStatusAndEmptyCollection_RoundTrips()
        {
            // Arrange
            InstalledPackagesResult expectedResult = NuGetContractsFactory.CreateInstalledPackagesResult(
                InstalledPackageResultStatus.Successful,
                new List<NuGetInstalledPackage>());

            // Act
            InstalledPackagesResult? actualResult = SerializeThenDeserialize(
                InstalledPackagesResultFormatter.Instance,
                expectedResult);

            // Assert
            actualResult.Should().NotBeNull();
            actualResult!.Status.Should().Be(InstalledPackageResultStatus.Successful);
            actualResult.Packages.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void SerializeThenDeserialize_WithMultipleInstalledPackages_RoundTrips()
        {
            // Arrange
            var packages = new List<NuGetInstalledPackage>
            {
                NuGetContractsFactory.CreateNuGetInstalledPackage("PackageA", "1.0.0", "1.0.0", @"C:\packages\PackageA.1.0.0", true),
                NuGetContractsFactory.CreateNuGetInstalledPackage("PackageB", "[2.0.0, 3.0.0)", "2.5.0", @"C:\packages\PackageB.2.5.0", false),
                NuGetContractsFactory.CreateNuGetInstalledPackage("PackageC", null, "1.2.3", null, true),
            };

            InstalledPackagesResult expectedResult = NuGetContractsFactory.CreateInstalledPackagesResult(
                InstalledPackageResultStatus.Successful,
                packages);

            // Act
            InstalledPackagesResult? actualResult = SerializeThenDeserialize(
                InstalledPackagesResultFormatter.Instance,
                expectedResult);

            // Assert
            actualResult.Should().NotBeNull();
            actualResult!.Status.Should().Be(InstalledPackageResultStatus.Successful);
            actualResult.Packages.Should().NotBeNull()
                .And.HaveCount(3);

            actualResult.Packages.Should().BeEquivalentTo(expectedResult.Packages, options => options
                .WithStrictOrdering()
                .ComparingByMembers<NuGetInstalledPackage>());
        }

        [Theory]
        [InlineData(InstalledPackageResultStatus.ProjectNotReady)]
        [InlineData(InstalledPackageResultStatus.ProjectInvalid)]
        [InlineData(InstalledPackageResultStatus.Unknown)]
        public void SerializeThenDeserialize_WithErrorStatus_RoundTrips(InstalledPackageResultStatus status)
        {
            // Arrange
            InstalledPackagesResult expectedResult = NuGetContractsFactory.CreateInstalledPackagesResult(
                status,
                null);

            // Act
            InstalledPackagesResult? actualResult = SerializeThenDeserialize(
                InstalledPackagesResultFormatter.Instance,
                expectedResult);

            // Assert
            actualResult.Should().NotBeNull();
            actualResult!.Status.Should().Be(status);
            actualResult.Packages.Should().BeNull();
        }

        [Fact]
        public void SerializeThenDeserialize_WithNullValue_RoundTrips()
        {
            // Act
            InstalledPackagesResult? actualResult = SerializeThenDeserialize(
                InstalledPackagesResultFormatter.Instance,
                null);

            // Assert
            actualResult.Should().BeNull();
        }
    }
}
