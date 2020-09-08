// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Threading;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    internal sealed class TestSharedServiceState : ISharedServiceState
    {
        public AsyncLazy<NuGetPackageManager> PackageManager { get; }
        public AsyncLazy<IVsSolutionManager> SolutionManager { get; }
        public AsyncLazy<ISourceRepositoryProvider> SourceRepositoryProvider { get; }

        internal TestSharedServiceState(
            AsyncLazy<NuGetPackageManager> packageManager,
            AsyncLazy<IVsSolutionManager> solutionManager,
            AsyncLazy<ISourceRepositoryProvider> sourceRepositoryProvider)
        {
            PackageManager = packageManager;
            SolutionManager = solutionManager;
            SourceRepositoryProvider = sourceRepositoryProvider;
        }
    }
}
