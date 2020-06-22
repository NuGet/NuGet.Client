// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.VisualStudio.Contracts
{
    /// <summary>Factory to create contract types</summary>
    /// <remarks>Trying to be forwards compatible with what C#9 records are going to be</remarks>
    public static class NuGetContractsFactory
    {
        /// <summary>Create a <see cref="NuGetInstalledPackage"/></summary>
        /// <param name="id">Package Id</param>
        /// <param name="requestedRange">The requested range</param>
        /// <param name="requestedVersion">The requested version</param>
        /// <returns><see cref="NuGetInstalledPackage"/></returns>
        public static NuGetInstalledPackage CreateNuGetInstalledPackage(string id, string requestedRange, string requestedVersion)
        {
            return new NuGetInstalledPackage(id, requestedRange, requestedVersion);
        }

        /// <summary>Create a <see cref="GetInstalledPackageResultStatus"/></summary>
        /// <param name="status"><see cref="GetInstalledPackageResultStatus"/></param>
        /// <param name="packages">Read-only collection of <see cref="NuGetInstalledPackage"/></param>
        /// <returns><see cref="GetInstalledPackagesResult"/></returns>
        public static GetInstalledPackagesResult CreateGetInstalledPackagesResult(GetInstalledPackageResultStatus status, IReadOnlyCollection<NuGetInstalledPackage> packages)
        {
            return new GetInstalledPackagesResult(status, packages);
        }
    }
}
