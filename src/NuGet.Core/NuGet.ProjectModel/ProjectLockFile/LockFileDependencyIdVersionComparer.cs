using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Common;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class LockFileDependencyIdVersionComparer : IEqualityComparer<LockFileDependency>
    {
        public bool Equals(LockFileDependency x, LockFileDependency y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null)
                || ReferenceEquals(y, null))
            {
                return false;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id) &&
                EqualityUtility.EqualsWithNullCheck(x.ResolvedVersion, y.ResolvedVersion);
        }

        public int GetHashCode(LockFileDependency obj)
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(obj.Id);
            combiner.AddObject(obj.ResolvedVersion);

            return combiner.CombinedHash;
        }
    }
}
