// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Threading.Tasks;
using System.Threading;

namespace NuGet.SolutionRestoreManager
{
    public interface IVsSolutionRestoreService5 : IVsSolutionRestoreService4
    {
        /// <summary>
        /// An entry point used by CPS to indicate given project needs to be restored.
        /// This entry point also handles PackageDownload items
        /// </summary>
        /// <param name="projectUniqueName">
        /// Unique identifier of the project. Should be a full path to project file.
        /// </param>
        /// <param name="projectRestoreInfo">Metadata <see cref="IVsProjectRestoreInfo2"/> needed for restoring the project.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>
        /// Returns a restore task corresponding to the nominated project request.
        /// NuGet will batch restore requests so it's possible the same restore task will be returned for multiple projects.
        /// When the requested restore operation for the given project completes the task will indicate operation success or failure.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="projectUniqueName" /> is not the path of a project file.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="projectRestoreInfo" /> is <see langword="null" />.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="token" /> is cancelled.</exception>
        Task<bool> NominateProjectAsync(string projectUniqueName, IVsProjectRestoreInfo3 projectRestoreInfo, CancellationToken token);
    }
}
