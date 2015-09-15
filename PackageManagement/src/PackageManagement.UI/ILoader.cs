// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public interface ILoader
    {
        // The second value tells us whether there are more items to load
        Task<LoadResult> LoadItemsAsync(int startIndex, CancellationToken ct);

        string LoadingMessage { get; }
    }
}
