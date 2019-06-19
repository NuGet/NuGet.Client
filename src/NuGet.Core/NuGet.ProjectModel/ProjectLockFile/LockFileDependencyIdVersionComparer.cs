// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.ProjectModel
{
    public class LockFileDependencyIdVersionComparer : IEqualityComparer<LockFileDependency>
    {
        public static LockFileDependencyIdVersionComparer Default { get; } = new LockFileDependencyIdVersionComparer();

        public bool Equals(LockFileDependency x, LockFileDependency y)
        {
            return LockFileDependency.Equals(x, y, LockFileDependency.ComparisonType.IdVersion);
        }

        public int GetHashCode(LockFileDependency obj)
        {
            return LockFileDependency.GetHashCode(obj, LockFileDependency.ComparisonType.IdVersion);
        }
    }
}
