// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.VisualStudio.Contracts
{
    /// <summary>Service to interact with projects in a solution</summary>
    /// <remarks>This interface should not be implemented. New methods may be added over time.</remarks>
    public interface INuGetProjectServices
    {
        /// <Summary>Gets the list of packages installed in a project.</Summary>
        /// <param name="projectId">Project ID (GUID).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The list of packages in the project.</returns>
        /// <exception cref="System.ArgumentException">When projectId is not a guid.</exception>
        Task<GetInstalledPackagesResult> GetInstalledPackagesAsync(string projectId, CancellationToken cancellationToken);
    }

    /// <summary>Result of a call to INuGetProjectServices.GetInstalledPackagesAsync</summary>
    public sealed class GetInstalledPackagesResult
    {
        /// <summary>The status of the result</summary>
        public GetInstalledPackageResultStatus Status { get; }

        /// <summary>List of packages in the project</summary>
        /// <remarks>May be null if <see cref="Status"/> was not successful</remarks>
        public IReadOnlyCollection<NuGetInstalledPackage> Packages { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="status"></param>
        /// <param name="packages"></param>
        internal GetInstalledPackagesResult(GetInstalledPackageResultStatus status, IReadOnlyCollection<NuGetInstalledPackage> packages)
        {
            Status = status;
            Packages = packages;
        }
    }

    /// <summary>The status of the result</summary>
    public enum GetInstalledPackageResultStatus
    {
        /// <summary>Unknown status</summary>
        /// <remarks>Probably represents a bug in the method that created the result.</remarks>
        Unknown = 0,

        /// <summary>Successful</summary>
        Successful,

        /// <summary>The project is not yet ready</summary>
        /// <remarks>This typically happens shortly after the project is loaded, but the project system has not yet informed NuGet about package references
        /// <ul>
        /// <li>item</li>
        /// </ul>
        /// </remarks>
        ProjectNotReady,

        /// <summary>Package information could not be retrieved because the project is in an invalid state</summary>
        /// <remarks>If a project has an invalid target framework value, or a package reference has a version value, NuGet may be unable to generate basic project information, such as requested packages.</remarks>
        ProjectInvalid
    }

    /// <summary>Basic information about a package</summary>
    public sealed class NuGetInstalledPackage
    {
        /// <summary>The package id.</summary>
        public string Id { get; }

        /// <summary>The project's request package range for the package.</summary>
        /// <remarks>
        /// If the project uses packages.config, this will be the installed package version.
        /// If the project uses PackageReference, this is the version string in the project file, which may not match the resolved package version.
        /// </remarks>
        public string RequestedRange { get; }

        // I'd love this class to be replaced with a record type once that feature is available in the language. Can we design this class to be forwards compatible with record types so it can be replaced in a future version?
        internal NuGetInstalledPackage(string id, string requestedRange)
        {
            Id = id;
            RequestedRange = requestedRange;
        }
    }

    /// <summary>Factory to create types</summary>
    /// <remarks>Trying to be forwards compatible with what C#9 records are going to be</remarks>
    public static class ContractsFactory
    {
        /// <summary>Create a <see cref="NuGetInstalledPackage"/></summary>
        /// <param name="id"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static NuGetInstalledPackage CreateNuGetInstalledPackage(string id, string version)
        {
            return new NuGetInstalledPackage(id, version);
        }

        /// <summary>Create a <see cref="GetInstalledPackageResultStatus"/></summary>
        /// <param name="status"></param>
        /// <param name="packages"></param>
        /// <returns></returns>
        public static GetInstalledPackagesResult CreateGetInstalledPackagesResult(GetInstalledPackageResultStatus status, IReadOnlyCollection<NuGetInstalledPackage> packages)
        {
            return new GetInstalledPackagesResult(status, packages);
        }
    }
}
