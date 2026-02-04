// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using NuGet.VisualStudio.Contracts;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.Formatters
{
    public sealed class NuGetInstalledPackageFormatterTests : FormatterTests
    {
        [Theory]
        [InlineData("PackageA", "1.0.0", "1.0.0", @"C:\packages\PackageA.1.0.0", true)]
        [InlineData("PackageB", "[2.0.0, 3.0.0)", "2.5.0", @"C:\packages\PackageB.2.5.0", false)]
        [InlineData("PackageC", null, "1.2.3", null, true)]
        [InlineData("PackageD", "1.0.0", null, @"C:\packages\PackageD", false)]
        [InlineData("PackageE", null, null, null, true)]
        public void SerializeThenDeserialize_WithValidArguments_RoundTrips(
            string id,
            string requestedRange,
            string version,
            string installPath,
            bool directDependency)
        {
            // Arrange
            NuGetInstalledPackage expectedResult = NuGetContractsFactory.CreateNuGetInstalledPackage(
                id,
                requestedRange,
                version,
                installPath,
                directDependency);

            // Act
            NuGetInstalledPackage? actualResult = SerializeThenDeserialize(
                formatter: NuGetInstalledPackageFormatter.Instance,
                expectedResult);

            // Assert
            actualResult.Should().NotBeNull();
            actualResult.Should().BeEquivalentTo(expectedResult, options => options
                .ComparingByMembers<NuGetInstalledPackage>());
        }

        [Fact]
        public void SerializeThenDeserialize_WithNullValue_RoundTrips()
        {
            // Act
            NuGetInstalledPackage? actualResult = SerializeThenDeserialize(
                NuGetInstalledPackageFormatter.Instance,
                null);

            // Assert
            actualResult.Should().BeNull();
        }
    }
}
