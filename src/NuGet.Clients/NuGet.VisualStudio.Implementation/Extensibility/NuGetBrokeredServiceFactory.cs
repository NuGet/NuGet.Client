// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Threading;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    public class NuGetBrokeredServiceFactory
    {
        private readonly AsyncLazy<IProjectSystemCache> _projectSystemCache;

        public NuGetBrokeredServiceFactory(AsyncLazy<IProjectSystemCache> projectSystemCache)
        {
            _projectSystemCache = projectSystemCache?? throw new ArgumentNullException(nameof(projectSystemCache));
        }

        public async ValueTask<object> CreateNuGetProjectServiceV1(ServiceMoniker moniker, ServiceActivationOptions options, IServiceBroker serviceBroker, CancellationToken cancellationToken)
        {
            var projectSystemCache = await _projectSystemCache.GetValueAsync(cancellationToken);

            return new ValueTask<object>(new NuGetProjectService(projectSystemCache));
        }
    }
}
