// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileDependency : IEquatable<LockFileDependency>
    {
        public string Id { get; set; }

        public NuGetVersion ResolvedVersion { get; set; }

        public VersionRange RequestedVersion { get; set; }

        public string ContentHash { get; set; }

        public PackageDependencyType Type { get; set; }

        public IList<PackageDependency> Dependencies { get; set; } = new List<PackageDependency>();

        public bool Equals(LockFileDependency other)
        {
            return Equals(this, other, ComparisonType.Full);
        }

        internal static bool Equals(LockFileDependency x, LockFileDependency y, ComparisonType comparisonType)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            switch (comparisonType)
            {
                case ComparisonType.IdVersion:
                    return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id) &&
                        EqualityUtility.EqualsWithNullCheck(x.ResolvedVersion, y.ResolvedVersion);

                case ComparisonType.ExcludeContentHash:
                    return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id) &&
                        EqualityUtility.EqualsWithNullCheck(x.ResolvedVersion, y.ResolvedVersion) &&
                        EqualityUtility.EqualsWithNullCheck(x.RequestedVersion, y.RequestedVersion) &&
                        EqualityUtility.SequenceEqualWithNullCheck(x.Dependencies, y.Dependencies) &&
                        x.Type == y.Type;

                case ComparisonType.Full:
                    return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id) &&
                        EqualityUtility.EqualsWithNullCheck(x.ResolvedVersion, y.ResolvedVersion) &&
                        EqualityUtility.EqualsWithNullCheck(x.RequestedVersion, y.RequestedVersion) &&
                        EqualityUtility.SequenceEqualWithNullCheck(x.Dependencies, y.Dependencies) &&
                        x.ContentHash == y.ContentHash &&
                        x.Type == y.Type;

                default:
                    throw new ArgumentException("Unknown ComparisonType value", nameof(comparisonType));
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LockFileDependency);
        }

        public override int GetHashCode()
        {
            return GetHashCode(this, ComparisonType.Full);
        }

        internal static int GetHashCode(LockFileDependency obj, ComparisonType comparisonType)
        {
            var combiner = new HashCodeCombiner();

            switch (comparisonType)
            {
                case ComparisonType.IdVersion:
                    combiner.AddObject(obj.Id);
                    combiner.AddObject(obj.ResolvedVersion);
                    break;

                case ComparisonType.ExcludeContentHash:
                    combiner.AddObject(obj.Id);
                    combiner.AddObject(obj.ResolvedVersion);
                    combiner.AddObject(obj.RequestedVersion);
                    combiner.AddSequence(obj.Dependencies);
                    combiner.AddObject(obj.Type);
                    break;

                case ComparisonType.Full:
                    combiner.AddObject(obj.Id);
                    combiner.AddObject(obj.ResolvedVersion);
                    combiner.AddObject(obj.RequestedVersion);
                    combiner.AddSequence(obj.Dependencies);
                    combiner.AddObject(obj.ContentHash);
                    combiner.AddObject(obj.Type);
                    break;

                default:
                    throw new ArgumentException("Unknown ComparisonType value", nameof(comparisonType));
            }

            return combiner.CombinedHash;
        }

        internal enum ComparisonType
        {
            IdVersion,
            ExcludeContentHash,
            Full
        }
    }
}
