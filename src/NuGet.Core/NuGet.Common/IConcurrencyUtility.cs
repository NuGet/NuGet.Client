// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Common
{
    public interface IConcurrencyUtility
    {
        Task<T> ExecuteWithFileLockedAsync<T>(
            string filePath,
            Func<CancellationToken, Task<T>> action,
            CancellationToken token);
    }
}
