// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    public class NuGetBrokeredServiceFactory
    {
        private readonly IAsyncServiceProvider _serviceProvider;
        private readonly JoinableTaskFactory _joinableTaskFactory;

        public NuGetBrokeredServiceFactory(IAsyncServiceProvider serviceProvider,
            JoinableTaskFactory joinableTaskFactory)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _joinableTaskFactory = joinableTaskFactory ?? throw new ArgumentNullException(nameof(joinableTaskFactory));
        }

        public ValueTask<object> CreateNuGetProjectServiceV1(ServiceMoniker moniker, ServiceActivationOptions options, IServiceBroker serviceBroker, CancellationToken cancellationToken)
        {
            return new ValueTask<object>(
                new NuGetProjectServices(
                    new Microsoft.VisualStudio.Threading.AsyncLazy<IProjectSystemCache>(async () =>
                    {
                        //var cache = _serviceProvider.GetServiceAsync<IProjectSystemCache>();
                        var cache = await ServiceLocator.GetInstanceAsync<IProjectSystemCache>();
                        return cache;
                    }, _joinableTaskFactory)));
        }
    }
}
