// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Microsoft.VisualStudio.Threading;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface ISharedServiceState
    {
        AsyncLazy<NuGetPackageManager> PackageManager { get; }
        AsyncLazy<IVsSolutionManager> SolutionManager { get; }
        AsyncLazy<ISourceRepositoryProvider> SourceRepositoryProvider { get; }
    }
}
