// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.ProjectModel.ProjectLockFile
{
    public class LockFileDependencyComparerWithoutContentHash : IEqualityComparer<LockFileDependency>
    {
        public static LockFileDependencyComparerWithoutContentHash Default { get; } = new LockFileDependencyComparerWithoutContentHash();

        public bool Equals(LockFileDependency x, LockFileDependency y)
        {
            return LockFileDependency.Equals(x, y, LockFileDependency.ComparisonType.ExcludeContentHash);
        }

        public int GetHashCode(LockFileDependency obj)
        {
            return LockFileDependency.GetHashCode(obj, LockFileDependency.ComparisonType.ExcludeContentHash);
        }
    }
}
