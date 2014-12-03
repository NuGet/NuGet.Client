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

        public CompatibilityProvider(IFrameworkNameProvider mappings)
        {
            _mappings = mappings;
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

            var frameworkComparer = new NuGetFrameworkNameComparer();
            var profileComparer = new NuGetFrameworkProfileComparer();

            var eqFramework = _mappings.GetEquivalentFrameworks(framework);
            var eqOther = _mappings.GetEquivalentFrameworks(other);

            foreach (var fw in eqFramework)
            {
                foreach (var otherFw in eqOther)
                {
                    if (frameworkComparer.Equals(fw, otherFw) && profileComparer.Equals(fw, otherFw))
                    {
                        return IsVersionCompatible(framework, other);
                    }
                }
            }

            return false;
        }

        private bool IsVersionCompatible(NuGetFramework framework, NuGetFramework other)
        {
            return other.AllVersions || other.Version <= framework.Version;
        }
    }
}
