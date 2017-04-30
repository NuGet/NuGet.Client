// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class DiagnosticUtilityTests
    {
        [Fact]
        public void GivenIFormatAnExpectedIdentityVerifyOutputString()
        {
            var range = VersionRange.Parse("1.0");

            DiagnosticUtility.FormatExpectedIdentity("A", range).Should().Be("A 1.0.0");
        }

        [Fact]
        public void GivenIFormatAnExpectedIdentityWithNoBoundsVerifyIdOnly()
        {
            DiagnosticUtility.FormatExpectedIdentity("A", VersionRange.All).Should().Be("A");
        }

        [Fact]
        public void GivenIFormatAnExpectedIdentityWithNoLowerBoundVerifyIdOnly()
        {
            var range = VersionRange.Parse("(1.0.0, 2.0.0]");

            DiagnosticUtility.FormatExpectedIdentity("A", range).Should().Be("A");
        }

        [Fact]
        public void GivenIFormatADependencyVerifyOutputString()
        {
            var range = VersionRange.Parse("1.0.0");

            DiagnosticUtility.FormatDependency("A", range).Should().Be("A (>= 1.0.0)");
        }

        [Fact]
        public void GivenIFormatADependencyWithNoBoundsVerifyRangeNotShown()
        {
            var range = VersionRange.All;

            DiagnosticUtility.FormatDependency("A", range).Should().Be("A");
        }

        [Fact]
        public void GivenIFormatADependencyWithANullRangeVerifyRangeNotShown()
        {
            DiagnosticUtility.FormatDependency("A", range: null).Should().Be("A");
        }

        [Fact]
        public void GivenAProjectLibraryVerifyFormatDoesNotIncludeTheVersion()
        {
            var library = new LibraryIdentity("A", NuGetVersion.Parse("1.0.0"), LibraryType.Project);

            DiagnosticUtility.FormatIdentity(library).Should().Be("A", "the version is not needed for projects");
        }

        [Fact]
        public void GivenAPackageLibraryVerifyFormatDoesIncludeTheVersion()
        {
            var library = new LibraryIdentity("A", NuGetVersion.Parse("1.0.0"), LibraryType.Package);

            DiagnosticUtility.FormatIdentity(library).Should().Be("A 1.0.0", "the version is shown for packages");
        }

        [Fact]
        public void GivenAnUnresolvedLibraryVerifyFormatDoesNotIncludeTheVersion()
        {
            var library = new LibraryIdentity("A", null, LibraryType.Unresolved);

            DiagnosticUtility.FormatIdentity(library).Should().Be("A", "the version is not used for non-projects");
        }

        [Fact]
        public void GivenAPackageLibraryVerifyFormatNormalizesTheVersion()
        {
            var library = new LibraryIdentity("A", NuGetVersion.Parse("1.0+abc"), LibraryType.Package);

            DiagnosticUtility.FormatIdentity(library).Should().Be("A 1.0.0");
        }
    }
}
