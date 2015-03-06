using NuGet.Frameworks;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;

// When we're internalized into other projects, there are multiple HashCodeCombiners defined
// So make sure we select the right one
using HashCodeCombiner = NuGet.Packaging.Core.HashCodeCombiner;

namespace NuGet.Packaging
{
    /// <summary>
    /// A group of items/files from a nupkg with the same target framework.
    /// </summary>
    public class FrameworkSpecificGroup : IEquatable<FrameworkSpecificGroup>, IFrameworkSpecific
    {
        private readonly NuGetFramework _targetFramework;
        private readonly IEnumerable<string> _items;

        public FrameworkSpecificGroup(string targetFramework, IEnumerable<string> items)
        {
            if (items == null)
            {
                throw new ArgumentException("items");
            }

            if (String.IsNullOrEmpty(targetFramework))
            {
                _targetFramework = NuGetFramework.AnyFramework;
            }
            else
            {
                _targetFramework = NuGetFramework.Parse(targetFramework);
            }

            _items = items;
        }

        public FrameworkSpecificGroup(NuGetFramework targetFramework, IEnumerable<string> items)
        {
            if (targetFramework == null)
            {
                throw new ArgumentException("framework");
            }

            if (items == null)
            {
                throw new ArgumentException("items");
            }

            _targetFramework = targetFramework;
            _items = items;
        }

        /// <summary>
        /// Group target framework
        /// </summary>
        public NuGetFramework TargetFramework
        {
            get
            {
                return _targetFramework;
            }
        }

        /// <summary>
        /// Item relative paths
        /// </summary>
        public IEnumerable<string> Items
        {
            get
            {
                return _items;
            }
        }

        public bool Equals(FrameworkSpecificGroup other)
        {
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (Object.ReferenceEquals(other, null))
            {
                return false;
            }

            return GetHashCode() == other.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            FrameworkSpecificGroup other = obj as FrameworkSpecificGroup;

            if (other != null)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            if (Object.ReferenceEquals(this, null))
            {
                return 0;
            }

            HashCodeCombiner combiner = new HashCodeCombiner();

            combiner.AddObject(TargetFramework);

            if (Items != null)
            {
                foreach (int hash in Items.Select(e => e.GetHashCode()).OrderBy(e => e))
                {
                    combiner.AddObject(hash);
                }
            }

            return combiner.CombinedHash;
        }
    }
}
