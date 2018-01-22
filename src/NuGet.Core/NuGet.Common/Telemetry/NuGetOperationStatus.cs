// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    /// <summary>
    /// Define different states for nuget operation status.
    /// </summary>
    public enum NuGetOperationStatus
    {
        /// <summary>
        /// no operation performed.
        /// </summary>
        NoOp = 0,

        /// <summary>
        /// operation was successful.
        /// </summary>
        Succeeded = 1,

        /// <summary>
        /// operation failed.
        /// </summary>
        Failed = 2,

        /// <summary>
        /// operation was cancelled
        /// </summary>
        Cancelled = 3
    }
}
