using NuGet.Frameworks;

namespace NuGet.Client
{
    /// <summary>
    /// An internal NuGetFramework marker for ManagedCodeConventions.
    /// Most conventions disallow the string 'any' as a txm, so to allow
    /// it for conventions with no txm in the path we use this special type.
    /// </summary>
    internal class AnyFramework : NuGetFramework
    {
        internal static AnyFramework Instance { get; } = new AnyFramework();

        private AnyFramework()
            : base(NuGetFramework.AnyFramework)
        {
        }
    }
}
