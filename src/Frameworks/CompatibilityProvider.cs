using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public class CompatibilityProvider : IFrameworkCompatibilityProvider
    {
        private readonly IFrameworkNameProvider _mappings;
        private readonly FrameworkExpander _expander;
        private static readonly Version _emptyVersion = new Version(0, 0, 0, 0);

        public CompatibilityProvider(IFrameworkNameProvider mappings)
        {
            _mappings = mappings;
            _expander = new FrameworkExpander(mappings);
        }

        public bool IsCompatible(NuGetFramework framework, NuGetFramework other)
        {
            // TODO: how should the special frameworks be handled?

            // unsupported should not be compatible with anything
            if (framework.IsUnsupported || other.IsUnsupported || framework.IsEmpty || other.IsEmpty)
            {
                return false;
            }

            if (framework.IsAny || other.IsAny)
            {
                return true;
            }

            // only a PCL can work in a PCL
            if (framework.IsPCL && !other.IsPCL)
            {
                return false;
            }

            if (other.IsPCL)
            {
                // perform the full check
                throw new NotImplementedException();
            }

            var fullComparer = new NuGetFrameworkFullComparer();

            // check if they are the exact same
            if (fullComparer.Equals(framework, other))
            {
                return true;
            }

            // find all possible substitutions
            HashSet<NuGetFramework> frameworkSet = new HashSet<NuGetFramework>(NuGetFramework.Comparer) { framework };

            foreach (var fw in _expander.Expand(framework))
            {
                frameworkSet.Add(fw);
            }

            var frameworkComparer = new NuGetFrameworkNameComparer();
            var profileComparer = new NuGetFrameworkProfileComparer();

            // check all possible substitutions
            foreach (var curFramework in frameworkSet)
            {
                // compare the frameworks
                if (frameworkComparer.Equals(curFramework, other)
                    && profileComparer.Equals(curFramework, other)
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
            return other == _emptyVersion || other <= framework;
        }
    }
}
