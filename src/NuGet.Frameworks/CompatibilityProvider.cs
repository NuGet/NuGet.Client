using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HashCombiner = NuGet.Frameworks.HashCodeCombiner;

namespace NuGet.Frameworks
{
    public class CompatibilityProvider : IFrameworkCompatibilityProvider
    {
        private readonly IFrameworkNameProvider _mappings;
        private readonly FrameworkExpander _expander;
        private static readonly NuGetFrameworkFullComparer _fullComparer = new NuGetFrameworkFullComparer();
        private readonly ConcurrentDictionary<int, bool> _cache;

        public CompatibilityProvider(IFrameworkNameProvider mappings)
        {
            _mappings = mappings;
            _expander = new FrameworkExpander(mappings);
            _cache = new ConcurrentDictionary<int, bool>();
        }

        /// <summary>
        /// Check if the frameworks are compatible.
        /// </summary>
        /// <param name="framework">Project framework</param>
        /// <param name="other">Other framework to check against the project framework</param>
        /// <returns>True if framework supports other</returns>
        public virtual bool IsCompatible(NuGetFramework framework, NuGetFramework other)
        {
            if (framework == null)
            {
                throw new ArgumentNullException("framework");
            }

            if (other == null)
            {
                throw new ArgumentNullException("other");
            }

            // check the cache for a solution
            int cacheKey = GetCacheKey(framework, other);

            bool? result = _cache.GetOrAdd(cacheKey, (key) =>
            {
                return IsCompatibleCore(framework, other) == true;
            });

            return result == true;
        }

        /// <summary>
        /// Actual compatibility check without caching
        /// </summary>
        protected virtual bool? IsCompatibleCore(NuGetFramework framework, NuGetFramework other)
        {
            bool? result = null;

            // check if they are the exact same
            if (_fullComparer.Equals(framework, other))
            {
                return true;
            }

            // special cased frameworks
            if (!framework.IsSpecificFramework || !other.IsSpecificFramework)
            {
                result = SpecialFrameworkCompare(framework, other);
            }

            if (result == null)
            {
                // PCL compat logic
                if (framework.IsPCL || other.IsPCL)
                {
                    result = PCLCompare(framework, other);
                }
                else
                {
                    // regular framework compat check
                    result = FrameworkCompare(framework, other);
                }
            }

            return result;
        }

        protected virtual bool? SpecialFrameworkCompare(NuGetFramework framework, NuGetFramework other)
        {
            // TODO: Revist these
            if (framework.IsAny || other.IsAny)
            {
                return true;
            }

            if (framework.IsUnsupported)
            {
                return false;
            }

            if (other.IsAgnostic)
            {
                return true;
            }

            if (other.IsUnsupported)
            {
                return false;
            }

            return null;
        }

        protected virtual bool? PCLCompare(NuGetFramework framework, NuGetFramework other)
        {
            // TODO: PCLs can only depend on other PCLs?
            if (framework.IsPCL && !other.IsPCL)
            {
                return false;
            }

            IEnumerable<NuGetFramework> frameworks = null;
            IEnumerable<NuGetFramework> otherFrameworks = null;

            if (framework.IsPCL)
            {
                // do not include optional frameworks here since we might be unable to tell what is optional on the other framework
                _mappings.TryGetPortableFrameworks(framework.Profile, false, out frameworks);
            }
            else
            {
                frameworks = new NuGetFramework[] { framework };
            }

            if (other.IsPCL)
            {
                // include optional frameworks here, the larger the list the more compatible it is
                _mappings.TryGetPortableFrameworks(other.Profile, true, out otherFrameworks);
            }
            else
            {
                otherFrameworks = new NuGetFramework[] { other };
            }

            // check if we this is a compatible superset
            return PCLInnerCompare(frameworks, otherFrameworks);
        }

        private bool? PCLInnerCompare(IEnumerable<NuGetFramework> profileFrameworks, IEnumerable<NuGetFramework> otherProfileFrameworks)
        {
            // TODO: Does this check need to make sure multiple frameworks aren't matched against a single framework from the other list?
            return profileFrameworks.Count() <= otherProfileFrameworks.Count() && profileFrameworks.All(f => otherProfileFrameworks.Any(ff => IsCompatible(f, ff)));
        }

        protected virtual bool? FrameworkCompare(NuGetFramework framework, NuGetFramework other)
        {
            // find all possible substitutions
            HashSet<NuGetFramework> frameworkSet = new HashSet<NuGetFramework>(NuGetFramework.Comparer) { framework };

            foreach (var fw in _expander.Expand(framework))
            {
                frameworkSet.Add(fw);
            }

            // check all possible substitutions
            foreach (var curFramework in frameworkSet)
            {
                // compare the frameworks
                if (NuGetFramework.FrameworkNameComparer.Equals(curFramework, other)
                    && StringComparer.OrdinalIgnoreCase.Equals(curFramework.Profile, other.Profile)
                    && IsVersionCompatible(curFramework, other))
                {
                    // allow the other if it doesn't have a platform
                    if (other.AnyPlatform)
                    {
                        return true;
                    }

                    // compare platforms
                    if (StringComparer.OrdinalIgnoreCase.Equals(curFramework.Platform, other.Platform))
                    {
                        return IsVersionCompatible(curFramework.PlatformVersion, other.PlatformVersion);
                    }
                }
            }

            return false;
        }

        private bool IsVersionCompatible(NuGetFramework framework, NuGetFramework other)
        {
            return IsVersionCompatible(framework.Version, other.Version);
        }

        private bool IsVersionCompatible(Version framework, Version other)
        {
            return other == FrameworkConstants.EmptyVersion || other <= framework;
        }

        private static int GetCacheKey(NuGetFramework framework, NuGetFramework other)
        {
            HashCombiner combiner = new HashCombiner();

            // create the cache key from the hash codes of both frameworks
            // the order is important here since compatibility is usually one way
            combiner.AddObject(framework);
            combiner.AddObject(other);

            return combiner.CombinedHash;
        }
    }
}
