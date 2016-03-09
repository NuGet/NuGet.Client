using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Shared;

namespace NuGet.Frameworks
{
    public class FallbackFramework : NuGetFramework, IEquatable<FallbackFramework>
    {
        /// <summary>
        /// List framework to fall back to.
        /// </summary>
        public IReadOnlyList<NuGetFramework> Fallback { get; }
        private int? _hashCode;

        public FallbackFramework(NuGetFramework framework, IReadOnlyList<NuGetFramework> fallbackFrameworks)
            : base(framework)
        {
            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            if (fallbackFrameworks == null)
            {
                throw new ArgumentNullException(nameof(fallbackFrameworks));
            }

            if (fallbackFrameworks.Count == 0)
            {
                throw new ArgumentException("Empty fallbackFrameworks is invalid",nameof(fallbackFrameworks));
            }

            Fallback = fallbackFrameworks;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FallbackFramework);
        }

        public override int GetHashCode()
        {
            if (_hashCode == null)
            {
                var combiner = new HashCodeCombiner();

                combiner.AddInt32(NuGetFramework.Comparer.GetHashCode(this));

                foreach (var each in Fallback)
                {
                    combiner.AddInt32(NuGetFramework.Comparer.GetHashCode(each));
                }

                _hashCode = combiner.CombinedHash;
            }

            return _hashCode.Value;
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
