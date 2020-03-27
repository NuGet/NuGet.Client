// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.SolutionRestoreManager
{
    internal class NuGetSolutionService : INuGetSolutionService
    {
        [Import]
        internal Lazy<ISolutionRestoreWorker> SolutionRestoreWorker { get; set; }

        private bool _initialized = false;

        public async Task RestoreSolutionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureInitializedAsync();
            await SolutionRestoreWorker.Value.ScheduleRestoreAsync(SolutionRestoreRequest.ByMenu(), cancellationToken);
        }

        private async Task EnsureInitializedAsync()
        {
            if (_initialized)
            {
                return;
            }
            var componentModel = await ServiceLocator.GetGlobalServiceAsync<SComponentModel, IComponentModel>();
            // ensure we satisfy our imports
            componentModel?.DefaultCompositionService.SatisfyImportsOnce(this);
            _initialized = true;
        }
        
    }
}
