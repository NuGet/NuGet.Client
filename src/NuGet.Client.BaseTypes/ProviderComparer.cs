using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Client
{
    /// <summary>
    /// An imperfect sort for provider before/after
    /// </summary>
    internal class ProviderComparer : IComparer<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>>
    {

        public ProviderComparer()
        {

        }

        // higher goes last
        public int Compare(Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata> providerA, Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata> providerB)
        {
            INuGetResourceProviderMetadata x = providerA.Metadata;
            INuGetResourceProviderMetadata y = providerB.Metadata;

            if (StringComparer.Ordinal.Equals(x.Name, y.Name))
            {
                return 0;
            }

            // empty names go last
            if (String.IsNullOrEmpty(x.Name))
            {
                return 1;
            }

            if (String.IsNullOrEmpty(y.Name))
            {
                return -1;
            }

            // check x 
            if (x.Before.Contains(y.Name, StringComparer.Ordinal))
            {
                return -1;
            }

            if (x.After.Contains(y.Name, StringComparer.Ordinal))
            {
                return 1;
            }

            // check y
            if (y.Before.Contains(x.Name, StringComparer.Ordinal))
            {
                return 1;
            }

            if (y.After.Contains(x.Name, StringComparer.Ordinal))
            {
                return -1;
            }

            // compare with the known names
            if ((x.Before.Contains(NuGetResourceProviderPositions.Last, StringComparer.Ordinal) || (x.After.Contains(NuGetResourceProviderPositions.Last, StringComparer.Ordinal)))
                && !(y.Before.Contains(NuGetResourceProviderPositions.Last, StringComparer.Ordinal) || (y.After.Contains(NuGetResourceProviderPositions.Last, StringComparer.Ordinal))))
            {
                return 1;
            }

            if ((y.Before.Contains(NuGetResourceProviderPositions.Last, StringComparer.Ordinal) || (y.After.Contains(NuGetResourceProviderPositions.Last, StringComparer.Ordinal)))
                && !(x.Before.Contains(NuGetResourceProviderPositions.Last, StringComparer.Ordinal) || (x.After.Contains(NuGetResourceProviderPositions.Last, StringComparer.Ordinal))))
            {
                return -1;
            }

            if ((x.Before.Contains(NuGetResourceProviderPositions.First, StringComparer.Ordinal) || (x.After.Contains(NuGetResourceProviderPositions.First, StringComparer.Ordinal)))
                && !(y.Before.Contains(NuGetResourceProviderPositions.First, StringComparer.Ordinal) || (y.After.Contains(NuGetResourceProviderPositions.First, StringComparer.Ordinal))))
            {
                return -1;
            }

            if ((y.Before.Contains(NuGetResourceProviderPositions.First, StringComparer.Ordinal) || (y.After.Contains(NuGetResourceProviderPositions.First, StringComparer.Ordinal)))
                && !(x.Before.Contains(NuGetResourceProviderPositions.First, StringComparer.Ordinal) || (x.After.Contains(NuGetResourceProviderPositions.First, StringComparer.Ordinal))))
            {
                return 1;
            }

            // give up and sort based on the name
            return StringComparer.Ordinal.Compare(x.Name, y.Name);
        }
    }
}
