// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.VisualStudio.Contracts
{
    /// <summary>Information about an installed package</summary>
    public sealed class NuGetInstalledPackage
    {
        /// <summary>The package id.</summary>
        public string Id { get; }

        /// <summary>The project's requested package range for the package.</summary>
        /// <remarks>
        /// If the project uses packages.config, this will be same as the installed package version.
        /// If the project uses PackageReference, this is the version string in the project file, which may not match the resolved package version, and may not be single version string.
        /// </remarks>
        public string RequestedRange { get; }

        /// <summary>The installed package version</summary>
        /// <remarks>
        /// If the project uses packages.config, this will be the same as requested range.
        /// If the project uses PackageReference, this will be the resolved version.
        /// </remarks>
        public string Version { get; }

        /// <summary>Path to the extracted package</summary>
        /// <remarks>When Visual Studio is connected to a Codespaces or Live Share environment, the path will be for the remote envionrment, not local.</remarks>
        public string InstallPath { get; }

        // This class will hopefully use C# record types when that language feature becomes available, so make the constructor not-public, to prevent breaking change when records come out.
        internal NuGetInstalledPackage(string id, string requestedRange, string version, string installPath)
        {
            Id = id;
            RequestedRange = requestedRange;
            Version = version;
            InstallPath = installPath;
        }
    }
}
