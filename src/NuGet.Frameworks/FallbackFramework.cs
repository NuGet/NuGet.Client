using System;
using System.Linq;
using NuGet.Common;
using NuGet.Frameworks;

namespace NuGet.Frameworks
{
    public class FallbackFramework : NuGetFramework, IEquatable<FallbackFramework>
    {
        /// <summary>
        /// Secondary framework to fall back to.
        /// </summary>
        public NuGetFramework Fallback { get; }

        public FallbackFramework(NuGetFramework framework, NuGetFramework fallbackFramework)
            : base(framework)
        {
            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            if (fallbackFramework == null)
            {
                throw new ArgumentNullException(nameof(fallbackFramework));
            }

            Fallback = fallbackFramework;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FallbackFramework);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddInt32(NuGetFramework.Comparer.GetHashCode(this));
            combiner.AddInt32(NuGetFramework.Comparer.GetHashCode(Fallback));

            return combiner.CombinedHash;
        }

        public bool Equals(FallbackFramework other)
        {
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            return NuGetFramework.Comparer.Equals(this, other)
                && NuGetFramework.Comparer.Equals(Fallback, other.Fallback);
        }
    }
}
