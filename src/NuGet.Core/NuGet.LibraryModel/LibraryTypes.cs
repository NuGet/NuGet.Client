// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.LibraryModel
{
    public static class LibraryTypes
    {
        /// <summary>
        /// Indicates that the library comes from compiling an XRE-based Project
        /// </summary>
        public const string Project = "project";

        /// <summary>
        /// Indicates that the library comes from compiling an external project (such as an MSBuild-based project)
        /// </summary>
        public const string ExternalProject = "externalProject";

        /// <summary>
        /// Indicates that the library comes from a NuGet Package
        /// </summary>
        public const string Package = "package";

        /// <summary>
        /// Indicates that the library comes from a stand-alone .NET Assembly
        /// </summary>
        public const string Assembly = "assembly";

        /// <summary>
        /// Indicates that the library comes from a .NET Assembly in a globally-accessible
        /// location such as the GAC or the Framework Reference Assemblies
        /// </summary>
        public const string Reference = "reference";

        /// <summary>
        /// Indicates that the library comes from a Windows Metadata Assembly (.winmd file)
        /// </summary>
        public const string WinMD = "winmd";

        /// <summary>
        /// Indicates that the library could not be resolved
        /// </summary>
        public const string Unresolved = "unresolved";
    }
}
