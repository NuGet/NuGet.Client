// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using FluentAssertions;
using NuGet.Versioning;
using Xunit;
using PackageWithVersion = NuGet.CommandLine.XPlat.Commands.Package.PackageArgument<NuGet.Versioning.NuGetVersion>;
using PackageWithVersionRange = NuGet.CommandLine.XPlat.Commands.Package.PackageArgument<NuGet.Versioning.VersionRange>;

namespace NuGet.CommandLine.Xplat.Tests.Utility
{
    public class PackageArgumentParserUtilityTest
    {
        private readonly RootCommand _versionRangeCommand;
        private readonly Argument<IReadOnlyList<PackageWithVersionRange>> _versionRangePackagesArgument;
        private readonly RootCommand _versionCommand;
        private readonly Argument<IReadOnlyList<PackageWithVersion>> _versionPackagesArgument;

        public PackageArgumentParserUtilityTest()
        {
            _versionRangeCommand = new RootCommand();

            _versionRangePackagesArgument = new Argument<IReadOnlyList<PackageWithVersionRange>>("packages")
            {
                Arity = ArgumentArity.ZeroOrMore,
                CustomParser = XPlat.Utility.PackageArgumentParserUtility.ParseWithVersionRange
            };

            _versionRangeCommand.Arguments.Add(_versionRangePackagesArgument);

            _versionCommand = new RootCommand();
            _versionPackagesArgument = new Argument<IReadOnlyList<PackageWithVersion>>("packages")
            {
                Arity = ArgumentArity.ZeroOrMore,
                CustomParser = XPlat.Utility.PackageArgumentParserUtility.ParseWithVersion
            };
            _versionCommand.Arguments.Add(_versionPackagesArgument);
        }

        [Fact]
        public void Parse_OnePackage_ReturnsListOfOne()
        {
            // Arrange
            var result = _versionRangeCommand.Parse("packageId");

            // Act
            var packages = result.GetValue(_versionRangePackagesArgument);

            // Assert
            packages.First().Id.Should().Be("packageId");
            packages.First().Version.Should().BeNull();
        }

        [Fact]
        public void Parse_TwoPackages_ReturnsListOfTwo()
        {
            // Arrange
            var result = _versionRangeCommand.Parse("packageId1 packageId2");

            // Act
            var packages = result.GetValue(_versionRangePackagesArgument);

            // Assert
            packages.Count.Should().Be(2);
            packages[0].Id.Should().Be("packageId1");
            packages[0].Version.Should().BeNull();
            packages[1].Id.Should().Be("packageId2");
            packages[1].Version.Should().BeNull();
        }

        [Fact]
        public void Parse_PackageWithVersion_ReturnsPackageWithVersion()
        {
            // Arrange
            var result = _versionRangeCommand.Parse("packageId@1.2.3");

            // Act
            var packages = result.GetValue(_versionRangePackagesArgument);

            // Assert
            packages.Count.Should().Be(1);
            packages[0].Id.Should().Be("packageId");
            packages[0].Version.Should().Be(VersionRange.Parse("1.2.3"));
        }

        [Fact]
        public void Parse_PackageWithRangeSyntax_ReturnsPackageWithVersion()
        {
            // Arrange
            var result = _versionRangeCommand.Parse("packageId@[1.2.3,2.0.0)");

            // Act
            var packages = result.GetValue(_versionRangePackagesArgument);

            // Assert
            packages.Count.Should().Be(1);
            packages[0].Id.Should().Be("packageId");
            packages[0].Version.Should().Be(VersionRange.Parse("[1.2.3,2.0.0)"));
        }

        [Fact]
        public void Parse_VersionWithNoId_ReturnsError()
        {
            // Arrange & Act
            var result = _versionRangeCommand.Parse("@1.2.3");

            // Assert
            result.Errors.Should().ContainSingle();
        }

        [Fact]
        public void Parse_PackageWithInvalidVersion_ReturnsError()
        {
            // Arrange & Act
            var result = _versionRangeCommand.Parse("packageId@one");

            // Assert
            result.Errors.Should().ContainSingle();
        }

        [Fact]
        public void Parse_PackageWithExactVersion_ReturnsPackageWithExactVersion()
        {
            // Arrange
            var result = _versionCommand.Parse("packageId@1.2.3");

            // Act
            var packages = result.GetValue(_versionPackagesArgument);

            // Assert
            packages.Count.Should().Be(1);
            packages[0].Id.Should().Be("packageId");
            packages[0].Version.Should().Be(new NuGetVersion("1.2.3"));
        }

        [Fact]
        public void Parse_TwoPackagesWithExactVersions_ReturnsListOfTwo()
        {
            // Arrange
            var result = _versionCommand.Parse("packageId1@1.0.0 packageId2@2.3.4");

            // Act
            var packages = result.GetValue(_versionPackagesArgument);

            // Assert
            packages.Count.Should().Be(2);
            packages[0].Id.Should().Be("packageId1");
            packages[0].Version.Should().Be(new NuGetVersion("1.0.0"));
            packages[1].Id.Should().Be("packageId2");
            packages[1].Version.Should().Be(new NuGetVersion("2.3.4"));
        }

        [Fact]
        public void Parse_PackageWithoutVersion_ReturnsPackageWithNullVersion()
        {
            // Arrange & Act
            var result = _versionCommand.Parse("packageId");

            // Act
            var packages = result.GetValue(_versionPackagesArgument);

            // Assert
            packages.Count.Should().Be(1);
            packages[0].Id.Should().Be("packageId");
            packages[0].Version.Should().BeNull();
        }

        [Fact]
        public void Parse_ExactVersionWithNoId_ReturnsError()
        {
            // Arrange & Act
            var result = _versionCommand.Parse("@1.2.3");

            // Assert
            result.Errors.Should().ContainSingle();
        }

        [Fact]
        public void Parse_PackageWithRangeSyntax_ReturnsError()
        {
            // Arrange & Act
            var result = _versionCommand.Parse("packageId@[1.2.3,2.0.0)");

            // Assert
            result.Errors.Should().ContainSingle();
        }

        [Fact]
        public void Parse_PackageWithInvalidExactVersion_ReturnsError()
        {
            // Arrange & Act
            var result = _versionCommand.Parse("packageId@one");

            // Assert
            result.Errors.Should().ContainSingle();
        }

        [Fact]
        public void Equal_SameIdAndVersion_AreEqual()
        {
            // Arrange
            var package1 = _versionCommand.Parse("packageId@1.2.3").GetValue(_versionPackagesArgument).First();
            var package2 = _versionCommand.Parse("packageId@1.2.3").GetValue(_versionPackagesArgument).First();

            // Act
            var areEqual = package1.Equals(package1, package2);

            // Assert
            areEqual.Should().BeTrue();
        }

        [Fact]
        public void Equal_DifferentId_AreNotEqual()
        {
            // Arrange
            var package1 = _versionCommand.Parse("packageId1@1.2.3").GetValue(_versionPackagesArgument).First();
            var package2 = _versionCommand.Parse("packageId2@1.2.3").GetValue(_versionPackagesArgument).First();

            // Act
            var areEqual = package1.Equals(package1, package2);

            // Assert
            areEqual.Should().BeFalse();
        }
    }
}
