using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Shared;

namespace NuGet.RuntimeModel
{
    public class CompatibilityProfile : IEquatable<CompatibilityProfile>
    {
        public string Name { get; }
        public IList<FrameworkRuntimePair> RestoreContexts { get; }

        public CompatibilityProfile(string name)
            : this(name, Enumerable.Empty<FrameworkRuntimePair>())
        { }

        public CompatibilityProfile(string name, IEnumerable<FrameworkRuntimePair> restoreContexts)
        {
            Name = name;
            RestoreContexts = restoreContexts.ToList();
        }

        public override string ToString()
        {
            return $"{Name}: {string.Join(",", RestoreContexts)}";
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.GetHashCode(Name, RestoreContexts);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CompatibilityProfile);
        }

        public bool Equals(CompatibilityProfile other)
        {
            return other != null &&
                string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                RestoreContexts.OrderedEquals(other.RestoreContexts, r => r);
        }
    }
}