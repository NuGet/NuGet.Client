﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Represents a solution restore operation to be executed by the 
    /// <see cref="SolutionRestoreWorker"/>.
    /// Designed to be called only once during its lifetime.
    /// </summary>
    internal interface ISolutionRestoreJob
    {
        /// <summary>
        /// Restore job's entry point.
        /// </summary>
        /// <param name="request">Solution restore request.</param>
        /// <param name="jobContext">Job context shared between different jobs.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result of restore operation. True if it succeeded.</returns>
        Task<bool> ExecuteAsync(
            SolutionRestoreRequest request,
            SolutionRestoreJobContext jobContext,
            RestoreOperationLogger logger,
            CancellationToken token);
    }
}
