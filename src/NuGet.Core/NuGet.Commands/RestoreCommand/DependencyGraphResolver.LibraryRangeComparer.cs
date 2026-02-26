// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.LibraryModel;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.Commands
{
    internal sealed partial class DependencyGraphResolver
    {
        /// <summary>
        /// Represents an <see cref="IEqualityComparer{T}" /> of <see cref="LibraryRange" /> that considers them to be equal based on the same functionality of <see cref="LibraryRange.ToString" />.
        /// </summary>
        public sealed class LibraryRangeComparer : IEqualityComparer<LibraryRange>
        {
            private LibraryRangeComparer()
            {
            }

            /// <summary>
            /// Gets an instance of <see cref="LibraryRangeComparer" />.
            /// </summary>
            public static LibraryRangeComparer Instance { get; } = new LibraryRangeComparer();

            public bool Equals(LibraryRange? x, LibraryRange? y)
            {
                if (x == null || y == null || x.VersionRange == null || y.VersionRange == null)
                {
                    return false;
                }

                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                return NormalizeTypeConstraint(x.TypeConstraint) == NormalizeTypeConstraint(y.TypeConstraint)
                    && x.Name.Equals(y.Name, StringComparison.OrdinalIgnoreCase)
                    && x.VersionRange.Equals(y.VersionRange);
            }

            public int GetHashCode(LibraryRange obj)
            {
                VersionRange versionRange = obj.VersionRange ?? VersionRange.None;

                HashCodeCombiner combiner = new();

                combiner.AddObject((int)NormalizeTypeConstraint(obj.TypeConstraint));
                combiner.AddStringIgnoreCase(obj.Name);
                combiner.AddObject(versionRange);

                return combiner.CombinedHash;
            }

            // All of this logic is copied from LibraryRange.ToString()
            private static LibraryDependencyTarget NormalizeTypeConstraint(LibraryDependencyTarget typeConstraint)
            {
                return typeConstraint switch
                {
                    LibraryDependencyTarget.Reference => LibraryDependencyTarget.Reference,
                    LibraryDependencyTarget.ExternalProject => LibraryDependencyTarget.ExternalProject,
                    LibraryDependencyTarget.Project => LibraryDependencyTarget.Project,
                    LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject => LibraryDependencyTarget.Project,
                    _ => LibraryDependencyTarget.None,
                };
            }
        }
    }
}
