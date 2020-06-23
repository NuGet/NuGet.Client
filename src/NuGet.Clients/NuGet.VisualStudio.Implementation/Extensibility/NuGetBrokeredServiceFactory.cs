// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    public class NuGetBrokeredServiceFactory
    {
        private readonly AsyncLazy<IVsSolutionManager> _solutionManager;

        public NuGetBrokeredServiceFactory(AsyncLazy<IVsSolutionManager> solutionManager)
        {
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
        }

        public async ValueTask<object> CreateNuGetProjectServiceV1(ServiceMoniker moniker, ServiceActivationOptions options, IServiceBroker serviceBroker, CancellationToken cancellationToken)
        {
            var solutionManager = await _solutionManager.GetValueAsync(cancellationToken);

            return new NuGetProjectService(solutionManager);
        }
    }
}
