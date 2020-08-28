// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    public sealed class SharedServiceState : ISharedServiceState
    {
        public AsyncLazy<NuGetPackageManager> PackageManager { get; }
        public AsyncLazy<IVsSolutionManager> SolutionManager { get; }
        public AsyncLazy<ISourceRepositoryProvider> SourceRepositoryProvider { get; }

        public SharedServiceState()
        {
            SolutionManager = new AsyncLazy<IVsSolutionManager>(
                ServiceLocator.GetInstanceAsync<IVsSolutionManager>,
                NuGetUIThreadHelper.JoinableTaskFactory);

            SourceRepositoryProvider = new AsyncLazy<ISourceRepositoryProvider>(
                ServiceLocator.GetInstanceAsync<ISourceRepositoryProvider>,
                NuGetUIThreadHelper.JoinableTaskFactory);

            PackageManager = new AsyncLazy<NuGetPackageManager>(
                async () =>
                {
                    IDeleteOnRestartManager deleteOnRestartManager = await ServiceLocator.GetInstanceAsync<IDeleteOnRestartManager>();
                    ISettings settings = await ServiceLocator.GetInstanceAsync<ISettings>();
                    IVsSolutionManager solutionManager = await SolutionManager.GetValueAsync(CancellationToken.None);
                    ISourceRepositoryProvider sourceRepositoryProvider = await SourceRepositoryProvider.GetValueAsync(CancellationToken.None);

                    return new NuGetPackageManager(
                        sourceRepositoryProvider,
                        settings,
                        solutionManager,
                        deleteOnRestartManager);
                },
                NuGetUIThreadHelper.JoinableTaskFactory);
        }
    }
}
