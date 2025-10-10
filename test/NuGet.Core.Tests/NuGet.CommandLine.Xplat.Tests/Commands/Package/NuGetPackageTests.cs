// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.CommandLine;
using FluentAssertions;
using NuGet.Versioning;
using Xunit;

using Pkg = NuGet.CommandLine.XPlat.Commands.Package.NuGetPackage;

namespace NuGet.CommandLine.Xplat.Tests.Commands.Package
{
    public class NuGetPackageTests
    {
        private RootCommand _versionRangeCommand;
        private RootCommand _exactVersionCommand;
        private Argument<IReadOnlyList<Pkg>> _versionRangePackagesArgument;
        private Argument<IReadOnlyList<Pkg>> _exactVersionPackagesArgument;

        public NuGetPackageTests()
        {
            _versionRangeCommand = new RootCommand();

            _versionRangePackagesArgument = new Argument<IReadOnlyList<Pkg>>("packages")
            {
                Arity = ArgumentArity.ZeroOrMore,
                CustomParser = Pkg.ParsePackagesWithVersionRange
            };
            _versionRangeCommand.Arguments.Add(_versionRangePackagesArgument);

            _exactVersionCommand = new RootCommand();
            _exactVersionPackagesArgument = new Argument<IReadOnlyList<Pkg>>("packages")
            {
                Arity = ArgumentArity.ZeroOrMore,
                CustomParser = Pkg.ParsePackagesWithExactVersions
            };
            _exactVersionCommand.Arguments.Add(_exactVersionPackagesArgument);
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

        [Fact]
        public void ParsePackagesWithExactVersions_PackageWithExactVersion_ReturnsPackageWithExactVersion()
        {
            // Arrange
            var result = _exactVersionCommand.Parse("packageId@1.2.3");

            // Act
            var packages = result.GetValue(_exactVersionPackagesArgument);

            // Assert
            IReadOnlyList<Pkg> expects = [new Pkg()
            {
                Id = "packageId",
                ExactVersion = new NuGetVersion("1.2.3"),
                VersionRange = null
            }];
            packages.Should().BeEquivalentTo(expects);
        }

        [Fact]
        public void ParsePackagesWithExactVersions_TwoPackagesWithExactVersions_ReturnsListOfTwo()
        {
            // Arrange
            var result = _exactVersionCommand.Parse("packageId1@1.0.0 packageId2@2.3.4");

            // Act
            var packages = result.GetValue(_exactVersionPackagesArgument);

            // Assert
            IReadOnlyList<Pkg> expects = [
                new Pkg() { Id = "packageId1", ExactVersion = new NuGetVersion("1.0.0") },
                new Pkg() { Id = "packageId2", ExactVersion = new NuGetVersion("2.3.4") }
            ];
            packages.Should().BeEquivalentTo(expects);
        }

        [Fact]
        public void ParsePackagesWithExactVersions_PackageWithoutVersion_ReturnsPackageWithNullVersion()
        {
            // Arrange & Act
            var result = _exactVersionCommand.Parse("packageId");

            // Act
            var packages = result.GetValue(_exactVersionPackagesArgument);

            // Assert
            IReadOnlyList<Pkg> expects = [new Pkg()
            {
                Id = "packageId",
                ExactVersion = null,
                VersionRange = null
            }];
            packages.Should().BeEquivalentTo(expects);
        }

        [Fact]
        public void ParsePackagesWithExactVersions_VersionWithNoId_ReturnsError()
        {
            // Arrange & Act
            var result = _exactVersionCommand.Parse("@1.2.3");

            // Assert
            result.Errors.Should().ContainSingle();
        }

        [Fact]
        public void ParsePackagesWithExactVersions_PackageWithRangeSyntax_ReturnsError()
        {
            // Arrange & Act
            var result = _exactVersionCommand.Parse("packageId@[1.2.3,2.0.0)");

            // Assert
            result.Errors.Should().ContainSingle();
        }

        [Fact]
        public void ParsePackagesWithExactVersions_PackageWithInvalidVersion_ReturnsError()
        {
            // Arrange & Act
            var result = _exactVersionCommand.Parse("packageId@one");

            // Assert
            result.Errors.Should().ContainSingle();
        }
    }
}
