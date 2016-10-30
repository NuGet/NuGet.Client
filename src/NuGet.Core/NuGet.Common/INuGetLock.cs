// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Common
{
    /// <summary>
    /// Basic get/release interface for an Async lock.
    /// </summary>
    public interface INuGetLock
    {
        /// <summary>
        /// Execute in lock.
        /// </summary>
        Task<T> ExecuteAsync<T>(Func<Task<T>> asyncAction);

        /// <summary>
        /// Lock id
        /// </summary>
        string Id { get; }
    }
}
