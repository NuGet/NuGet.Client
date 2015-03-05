using System;
using NuGet.Frameworks;
using System.Collections.Generic;

namespace NuGet.Frameworks
{
    public static class NuGetFrameworkExtensions
    {
        /// <summary>
        /// True if the Framework is .NETFramework
        /// </summary>
        public static bool IsDesktop(this NuGetFramework framework)
        {
            return framework.Framework.Equals(FrameworkConstants.FrameworkIdentifiers.Net, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Return the item with the target framework nearest the project framework
        /// </summary>
        public static IFrameworkSpecific GetNearest(this IEnumerable<IFrameworkSpecific> items, NuGetFramework projectFramework)
        {
            return NuGetFrameworkUtility.GetNearest<IFrameworkSpecific>(items, projectFramework, e => e.TargetFramework);
        }
    }
}