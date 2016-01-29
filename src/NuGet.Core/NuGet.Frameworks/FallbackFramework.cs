using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Shared;

namespace NuGet.Frameworks
{
    public class FallbackFramework : NuGetFramework, IEquatable<FallbackFramework>
    {
        /// <summary>
        /// List of frameworks to fall back to.
        /// </summary>
        public IEnumerable<NuGetFramework> Fallback { get; }

        public FallbackFramework(NuGetFramework framework, IEnumerable<NuGetFramework> fallbackFramework)
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

            foreach (var each in Fallback)
            {
                combiner.AddInt32(NuGetFramework.Comparer.GetHashCode(each));
            }

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
                && Fallback.SequenceEqual(other.Fallback);
        }
    }
}
