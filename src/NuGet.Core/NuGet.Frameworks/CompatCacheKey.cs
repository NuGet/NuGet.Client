using System;

namespace NuGet.Frameworks
{
    /// <summary>
    /// Internal cache key used to store framework compatibility.
    /// </summary>
    internal class CompatCacheKey : IEquatable<CompatCacheKey>
    {
        public NuGetFramework Target { get; }
        public NuGetFramework Candidate { get; }

        private readonly int _hashCode;

        public CompatCacheKey(NuGetFramework target, NuGetFramework candidate)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            Target = target;
            Candidate = candidate;

            // This is designed to be cached, just get the hash up front
            var combiner = new HashCodeCombiner();
            combiner.AddObject(target);
            combiner.AddObject(candidate);
            _hashCode = combiner.CombinedHash;
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public bool Equals(CompatCacheKey other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Target.Equals(other.Target)
                && Candidate.Equals(other.Candidate);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CompatCacheKey);
        }

        public override string ToString()
        {
            return $"{Target.DotNetFrameworkName} -> {Candidate.DotNetFrameworkName}";
        }
    }
}
