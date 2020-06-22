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

        /// <summary>The lowest version allowed by the <see cref="RequestedRange"/></summary>
        public string RequestedVersion { get; }

        // This class will hopefully use C# record types when that language feature becomes available, so make the constructor not-public, to prevent breaking change when records come out.
        internal NuGetInstalledPackage(string id, string requestedRange, string requestedVersion)
        {
            Id = id;
            RequestedRange = requestedRange;
            RequestedVersion = requestedVersion;
        }
    }
}
