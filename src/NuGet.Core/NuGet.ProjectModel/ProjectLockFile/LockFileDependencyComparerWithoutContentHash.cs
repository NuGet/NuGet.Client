// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Shared;

namespace NuGet.ProjectModel.ProjectLockFile
{
    public class LockFileDependencyComparerWithoutContentHash : IEqualityComparer<LockFileDependency>
    {
        public static LockFileDependencyComparerWithoutContentHash Default { get; } = new LockFileDependencyComparerWithoutContentHash();

        public bool Equals(LockFileDependency x, LockFileDependency y)
        {
            if (x == null || y == null)
            {
                return x == null && y == null;
            }

            if (ReferenceEquals(x, y))
            {
                return true;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id) &&
                EqualityUtility.EqualsWithNullCheck(x.ResolvedVersion, y.ResolvedVersion) &&
                EqualityUtility.EqualsWithNullCheck(x.RequestedVersion, y.RequestedVersion) &&
                EqualityUtility.SequenceEqualWithNullCheck(x.Dependencies, y.Dependencies) &&
                x.Type == y.Type;
        }

        public int GetHashCode(LockFileDependency obj)
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(obj.Id);
            combiner.AddObject(obj.ResolvedVersion);
            combiner.AddObject(obj.RequestedVersion);
            combiner.AddSequence(obj.Dependencies);
            combiner.AddObject(obj.Type);

            return combiner.CombinedHash;
        }
    }
}
