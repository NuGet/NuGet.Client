// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    public class NuGetBrokeredServiceFactory
    {
        private readonly AsyncLazy<IVsSolutionManager> _solutionManager;
        private readonly AsyncLazy<ISettings> _settings;

        public NuGetBrokeredServiceFactory(AsyncLazy<IVsSolutionManager> solutionManager, AsyncLazy<ISettings> settings)
        {
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async ValueTask<object> CreateNuGetProjectServiceV1(ServiceMoniker moniker, ServiceActivationOptions options, IServiceBroker serviceBroker, CancellationToken cancellationToken)
        {
            var solutionManager = await _solutionManager.GetValueAsync(cancellationToken);
            var settings = await _settings.GetValueAsync(cancellationToken);

            return new NuGetProjectService(solutionManager, settings);
        }
    }
}
