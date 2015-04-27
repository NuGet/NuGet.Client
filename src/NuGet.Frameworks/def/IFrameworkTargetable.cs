using System;
using System.Collections.Generic;

namespace NuGet.Frameworks
{
    /// <summary>
    /// Use this to expose the list of target frameworks an object can be used for.
    /// </summary>
    public interface IFrameworkTargetable
    {
        /// <summary>
        /// All frameworks supported by the parent
        /// </summary>
        IEnumerable<NuGetFramework> SupportedFrameworks { get; }
    }
}