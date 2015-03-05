using System;

namespace NuGet.Frameworks
{
    /// <summary>
    /// A group or object that is specific to a single target framework
    /// </summary>
    public interface IFrameworkSpecific
    {
        /// <summary>
        /// Target framework
        /// </summary>
        NuGetFramework TargetFramework { get; }
    }
}