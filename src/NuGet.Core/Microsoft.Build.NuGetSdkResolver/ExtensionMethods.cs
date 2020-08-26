using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.Build.NuGetSdkResolver
{
    internal static class ExtensionMethods
    {
        // In MSBuild 15.9, the SdkResolverContext has an Interactive property.  The value will be gotten through reflection so the MSBuild
        // references do not need to be updated.
        private static readonly PropertyInfo SdkResolverContextInteractivePropertyInfo = typeof(SdkResolverContext).GetRuntimeProperty("Interactive");

        /// <summary>
        /// Determines if the <see cref="SdkResolverContext"/> indicates if there should be no interactivity.
        /// </summary>
        /// <param name="context">A <see cref="SdkResolverContext"/> instance.</param>
        /// <returns><code>true</code> if there should be no interactivity, otherwise <code>false</code>.</returns>
        public static bool IsNonInteractive(this SdkResolverContext context)
        {
            // Determine if SdkResolverContext's Interactive property, defaulting to false
            var interactive = (bool?)SdkResolverContextInteractivePropertyInfo?.GetValue(context) ?? false;

            return !interactive;
        }
    }
}
