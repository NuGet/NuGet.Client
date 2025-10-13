// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.CommandLine;
using FluentAssertions;
using NuGet.Versioning;
using Xunit;

using Pkg = NuGet.CommandLine.XPlat.Commands.Package.NuGetPackageWithVersionRange;

namespace NuGet.CommandLine.Xplat.Tests.Commands.Package
{
    public class NuGetPackageWithVersionRangeTests
    {
        private RootCommand _versionRangeCommand;
        private Argument<IReadOnlyList<Pkg>> _versionRangePackagesArgument;

        public NuGetPackageWithVersionRangeTests()
        {
            _versionRangeCommand = new RootCommand();

            _versionRangePackagesArgument = new Argument<IReadOnlyList<Pkg>>("packages")
            {
                Arity = ArgumentArity.ZeroOrMore,
                CustomParser = Pkg.Parse
            };
            _versionRangeCommand.Arguments.Add(_versionRangePackagesArgument);
        }

        [Fact]
        public void ParsePackagesWithVersionRange_OnePackage_ReturnsListOfOne()
        {
            // Arrange
            var result = _versionRangeCommand.Parse("packageId");

            // Act
            var packages = result.GetValue(_versionRangePackagesArgument);

            // Assert
            IReadOnlyList<Pkg> expects = [new Pkg()
            {
                Id = "packageId",
                VersionRange = null
            }];
            packages.Should().BeEquivalentTo(expects);
        }

        [Fact]
        public void ParsePackagesWithVersionRange_TwoPackages_ReturnsListOfTwo()
        {
            // Arrange
            var result = _versionRangeCommand.Parse("packageId1 packageId2");

            // Act
            var packages = result.GetValue(_versionRangePackagesArgument);

            // Assert
            IReadOnlyList<Pkg> expects = [
                new Pkg() { Id = "packageId1", VersionRange = null },
                new Pkg() { Id = "packageId2", VersionRange = null }
            ];
            packages.Should().BeEquivalentTo(expects);
        }

        [Fact]
        public void ParsePackagesWithVersionRange_PackageWithVersion_ReturnsPackageWithVersion()
        {
            // Arrange
            var result = _versionRangeCommand.Parse("packageId@1.2.3");

            // Act
            var packages = result.GetValue(_versionRangePackagesArgument);

            // Assert
            IReadOnlyList<Pkg> expects = [new Pkg()
            {
                Id = "packageId",
                VersionRange = VersionRange.Parse("1.2.3")
            }];
            packages.Should().BeEquivalentTo(expects);
        }

        [Fact]
        public void ParsePackagesWithVersionRange_PackageWithRangeSyntax_ReturnsPackageWithVersion()
        {
            // Arrange
            var result = _versionRangeCommand.Parse("packageId@[1.2.3,2.0.0)");

            // Act
            var packages = result.GetValue(_versionRangePackagesArgument);

            // Assert
            IReadOnlyList<Pkg> expects = [new Pkg()
            {
                Id = "packageId",
                VersionRange = VersionRange.Parse("[1.2.3,2.0.0)")
            }];
            packages.Should().BeEquivalentTo(expects);
        }

        [Fact]
        public void ParsePackagesWithVersionRange_VersionWithNoId_ReturnsError()
        {
            // Arrange & Act
            var result = _versionRangeCommand.Parse("@1.2.3");

            // Assert
            result.Errors.Should().ContainSingle();
        }

        [Fact]
        public void ParsePackagesWithVersionRange_PackageWithInvalidVersion_ReturnsError()
        {
            // Arrange & Act
            var result = _versionRangeCommand.Parse("packageId@one");

            // Assert
            result.Errors.Should().ContainSingle();
        }
    }
}
