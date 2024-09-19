using System.Collections.Generic;
using FluentAssertions;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class DependencyGraphResolverTests
    {
        /// <summary>
        /// Verifies that the <see cref="DependencyGraphResolver.LibraryRangeComparer" /> calculates the same equality and hash code as <see cref="LibraryRange.ToString()" />.
        /// </summary>
        [Theory]
        [MemberData(nameof(TypeConstraintCombinations))]
        public void LibraryRangeComparer_AllTypeConstraints(LibraryRange libraryRange1, LibraryRange libraryRange2)
        {
            DependencyGraphResolver.LibraryRangeComparer comparer = DependencyGraphResolver.LibraryRangeComparer.Instance;

            string libraryRange1String = libraryRange1.ToString();
            string libraryRange2String = libraryRange2.ToString();

            var constraintString1 = string.Empty;

            switch (libraryRange1.TypeConstraint)
            {
                case LibraryDependencyTarget.Reference:
                    constraintString1 = LibraryType.Reference;
                    break;

                case LibraryDependencyTarget.ExternalProject:
                    constraintString1 = LibraryType.ExternalProject;
                    break;

                case LibraryDependencyTarget.Project:
                case LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject:
                    constraintString1 = LibraryType.Project;
                    break;
            }

            var constraintString2 = string.Empty;

            switch (libraryRange2.TypeConstraint)
            {
                case LibraryDependencyTarget.Reference:
                    constraintString2 = LibraryType.Reference;
                    break;

                case LibraryDependencyTarget.ExternalProject:
                    constraintString2 = LibraryType.ExternalProject;
                    break;

                case LibraryDependencyTarget.Project:
                case LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject:
                    constraintString2 = LibraryType.Project;
                    break;
            }

            // If the LibraryRange.ToString() is the same, then we expect DependencyGraphResolver.LibraryRangeComparer to also consider them equal
            if (string.Equals(constraintString1, constraintString2))
            {
                libraryRange1String.Should().Be(libraryRange2String);

                comparer.Equals(libraryRange1, libraryRange2).Should().BeTrue();

                comparer.GetHashCode(libraryRange1).Should().Be(comparer.GetHashCode(libraryRange2));
            }
            else
            {
                libraryRange1String.Should().NotBe(libraryRange2String);

                comparer.Equals(libraryRange1, libraryRange2).Should().BeFalse();

                comparer.GetHashCode(libraryRange1).Should().NotBe(comparer.GetHashCode(libraryRange2));
            }
        }

        /// <summary>
        /// Verifies that the <see cref="DependencyGraphResolver.LibraryRangeComparer" /> calculates the same equality and hash code as <see cref="VersionRange.ToNonSnapshotRange()" />.
        /// </summary>
        [Theory]
        [MemberData(nameof(VersionRangeCombinations))]
        public void LibraryRangeComparer_AllVersionRangeTypes(LibraryRange libraryRange1, LibraryRange libraryRange2)
        {
            DependencyGraphResolver.LibraryRangeComparer comparer = DependencyGraphResolver.LibraryRangeComparer.Instance;

            string libraryRange1String = libraryRange1.ToString();
            string libraryRange2String = libraryRange2.ToString();

            string versionString1 = libraryRange1.VersionRange!.ToNonSnapshotRange().PrettyPrint();

            string versionString2 = libraryRange2.VersionRange!.ToNonSnapshotRange().PrettyPrint();

            // If the version strings are the same, then we expect DependencyGraphResolver.LibraryRangeComparer to also consider them equal
            if (string.Equals(versionString1, versionString2))
            {
                libraryRange1String.Should().Be(libraryRange2String);

                comparer.Equals(libraryRange1, libraryRange2).Should().BeTrue();

                comparer.GetHashCode(libraryRange1).Should().Be(comparer.GetHashCode(libraryRange2));
            }
            else
            {
                libraryRange1String.Should().NotBe(libraryRange2String);

                comparer.Equals(libraryRange1, libraryRange2).Should().BeFalse();

                comparer.GetHashCode(libraryRange1).Should().NotBe(comparer.GetHashCode(libraryRange2));
            }
        }

        public static IEnumerable<object[]> TypeConstraintCombinations()
        {
            LibraryDependencyTarget[] typeConstraints =
            [
                LibraryDependencyTarget.All,
                LibraryDependencyTarget.ExternalProject,
                LibraryDependencyTarget.Package,
                LibraryDependencyTarget.Project,
                LibraryDependencyTarget.Reference,
                LibraryDependencyTarget.WinMD,
                LibraryDependencyTarget.PackageProjectExternal,
                LibraryDependencyTarget.None,
                LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject,
                LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject | LibraryDependencyTarget.Package
            ];

            string name = "PackageA";

            VersionRange versionRange = new VersionRange(new NuGetVersion(1, 0, 0));

            for (int i = 0; i < typeConstraints.Length; i++)
            {
                for (int x = 0; x < typeConstraints.Length; x++)
                {
                    LibraryDependencyTarget typeConstraint1 = typeConstraints[i];
                    LibraryDependencyTarget typeConstraint2 = typeConstraints[x];

                    yield return new object[]
                    {
                        new LibraryRange(name, versionRange, typeConstraint1),
                        new LibraryRange(name, versionRange, typeConstraint2),
                    };
                }
            }
        }

        public static IEnumerable<object[]> VersionRangeCombinations()
        {
            LibraryDependencyTarget typeConstraint = LibraryDependencyTarget.Package;

            string name = "PackageA";

            VersionRange[] versionRanges =
            [
                new VersionRange(new NuGetVersion(1, 0, 0)), // Min version
                new VersionRange(new NuGetVersion(1, 0, 0), true, new NuGetVersion(2, 0, 0), true), // Min and Max version
                new VersionRange(null, true, new NuGetVersion(2, 0, 0), true), // Max version
                VersionRange.Parse("10.1.*", allowFloating: true), // Floating version
                new VersionRange(NuGetVersion.Parse("1.0.0-beta")), // Prerelease version
                new VersionRange(NuGetVersion.Parse("1.0.0-beta"), true, NuGetVersion.Parse("2.0.0-beta"), true), // Prerelease min and max version
                new VersionRange(NuGetVersion.Parse("1.0.0-beta"), true, null, true), // Prerelease min version
                new VersionRange(null, true, NuGetVersion.Parse("2.0.0-beta"), true) // Prerelease max
            ];

            for (int i = 0; i < versionRanges.Length; i++)
            {
                for (int x = 0; x < versionRanges.Length; x++)
                {
                    VersionRange versionRange1 = versionRanges[i];
                    VersionRange versionRange2 = versionRanges[x];

                    yield return new object[]
                    {
                        new LibraryRange(name, versionRange1, typeConstraint),
                        new LibraryRange(name, versionRange2, typeConstraint),
                    };
                }
            }
        }
    }
}
