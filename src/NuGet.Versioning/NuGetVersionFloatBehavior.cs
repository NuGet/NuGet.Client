using System;

namespace NuGet.Versioning
{
    public enum NuGetVersionFloatBehavior
    {
        /// <summary>
        /// Lowest version, no float
        /// </summary>
        None,

        /// <summary>
        /// Highest matching pre-release label
        /// </summary>
        Prerelease,

        /// <summary>
        /// x.y.z.*
        /// </summary>
        Revision,

        /// <summary>
        /// x.y.*
        /// </summary>
        Patch,

        /// <summary>
        /// x.*
        /// </summary>
        Minor,

        /// <summary>
        /// *
        /// </summary>
        Major,

        /// <summary>
        /// Float major and pre-release
        /// </summary>
        AbsoluteLatest
    }
}