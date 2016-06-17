using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Shared;

#if IS_NET40_CLIENT
using FallbackList = System.Collections.Generic.IList<NuGet.Frameworks.NuGetFramework>;
#else
using FallbackList = System.Collections.Generic.IReadOnlyList<NuGet.Frameworks.NuGetFramework>;
#endif

namespace NuGet.Frameworks
{
#if NUGET_FRAMEWORKS_INTERNAL
    internal
#else
    public
#endif
    class FallbackFramework : NuGetFramework, IEquatable<FallbackFramework>
    {
        /// <summary>
        /// List framework to fall back to.
        /// </summary>
        public FallbackList Fallback
        {
            get { return _fallback; }
        }

        private readonly FallbackList _fallback;
        private int? _hashCode;

        public FallbackFramework(NuGetFramework framework, FallbackList fallbackFrameworks)
            : base(framework)
        {
            if (framework == null)
            {
                throw new ArgumentNullException("framework");
            }

            if (fallbackFrameworks == null)
            {
                throw new ArgumentNullException("fallbackFrameworks");
            }

            if (fallbackFrameworks.Count == 0)
            {
                throw new ArgumentException("Empty fallbackFrameworks is invalid", "fallbackFrameworks");
            }

            _fallback = fallbackFrameworks;
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
