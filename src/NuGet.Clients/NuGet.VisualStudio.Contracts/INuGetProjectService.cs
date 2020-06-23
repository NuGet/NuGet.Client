// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace NuGet.VisualStudio.Contracts
{
    /// <summary>Service to interact with projects in a solution</summary>
    /// <remarks>This interface should not be implemented. New methods may be added over time.</remarks>
    public interface INuGetProjectService
    {
        /// <Summary>Gets the list of packages installed in a project.</Summary>
        /// <param name="projectId">Project ID (GUID).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The list of packages in the project.</returns>
        /// <exception cref="RemoteInvocationException" />
        /// <remarks>
        /// When projectId is not a GUID, <see cref="RemoteInvocationException.ErrorCode"/> is set to <see cref="NuGetServices.ArgumentException"/>.
        /// </remarks>
        Task<InstalledPackagesResult> GetInstalledPackagesAsync(string projectId, CancellationToken cancellationToken);

    }
}
