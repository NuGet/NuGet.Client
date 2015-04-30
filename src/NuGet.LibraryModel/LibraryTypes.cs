using System;

namespace NuGet.LibraryModel
{
    public static class LibraryTypes
    {
        /// <summary>
        /// Indicates that the library comes from compiling an XRE-based Project
        /// </summary>
        public const string Project = "Project";

        /// <summary>
        /// Indicates that the library comes from compiling an external project (such as an MSBuild-based project)
        /// </summary>
        public const string ExternalProject = "ExternalProject";

        /// <summary>
        /// Indicates that the library comes from a NuGet Package
        /// </summary>
        public const string Package = "Package";

        /// <summary>
        /// Indicates that the library comes from a stand-alone .NET Assembly
        /// </summary>
        public const string Assembly = "Assembly";

        /// <summary>
        /// Indicates that the library comes from a .NET Assembly in a globally-accessible
        /// location such as the GAC or the Framework Reference Assemblies
        /// </summary>
        public const string Reference = "Reference";

        /// <summary>
        /// Indicates that the library comes from a Windows Metadata Assembly (.winmd file)
        /// </summary>
        public const string WinMD = "WinMD";
    }
}