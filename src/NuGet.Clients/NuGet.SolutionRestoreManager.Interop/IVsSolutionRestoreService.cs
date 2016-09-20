// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Represents a package restore contract for integration with a project system.
    /// </summary>
    [ComImport]
    [Guid("2b046428-ca39-40bb-8b4b-7dd1d96118cb")]
    public interface IVsSolutionRestoreService
    {
        /// <summary>
        /// This property will tell if last/current restore was a success or a failure
        /// </summary>
        Task<bool> CurrentRestoreOperation { get; }

        /// <summary>
        /// Returns if the requested nuget restore operation for the given project was a success
        /// or failure
        /// </summary>
        Task<bool> NominateProjectAsync(string projectUniqueName, IVsProjectRestoreInfo projectRestoreInfo, CancellationToken token);
    }
}
