// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement
{
    internal interface ISolutionPackagesContentHashUtility
    {
        Task<string> GetContentHashAsync(PackageIdentity packageIdentity, CancellationToken token);
    }
}
