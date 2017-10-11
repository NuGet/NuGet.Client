using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Utilities;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public static class PackageManagerProviderUtility
    {
        public static List<IVsPackageManagerProvider> Sort(
            IEnumerable<Lazy<IVsPackageManagerProvider, IOrderable>> packageManagerProviders, 
            int max)
        {
            var sortedProviders = new List<IVsPackageManagerProvider>();
            var uniqueId = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in Orderer.Order(packageManagerProviders))
            {
                if (sortedProviders.Count < max && !uniqueId.Contains(provider.Value.PackageManagerId))
                {
                    uniqueId.Add(provider.Value.PackageManagerId);
                    sortedProviders.Add(provider.Value);
                }
            }

            return sortedProviders;
        }
    }
}
