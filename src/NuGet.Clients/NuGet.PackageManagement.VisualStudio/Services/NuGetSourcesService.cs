// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    public class NuGetSourcesService : INuGetSourcesService
    {
        private bool _disposedValue;
        private readonly ServiceActivationOptions _options;
        private readonly IServiceBroker _serviceBroker;
        private readonly AuthorizationServiceClient _authorizationServiceClient;

        public NuGetSourcesService(ServiceActivationOptions options, IServiceBroker serviceBroker, AuthorizationServiceClient authorizationServiceClient, CancellationToken cancellationToken)
        {
            _options = options;
            _serviceBroker = serviceBroker;
            _authorizationServiceClient = authorizationServiceClient;
        }

        public async ValueTask<IReadOnlyList<PackageSource>> GetPackageSourcesAsync(CancellationToken cancellationToken)
        {
            var packageSources = await ServiceLocator.GetInstanceAsync<ISourceRepositoryProvider>();
            Assumes.NotNull(packageSources);
            return packageSources.PackageSourceProvider.LoadPackageSources().ToList();
        }

        public async ValueTask SavePackageSourcesAsync(IReadOnlyList<PackageSource> sources, CancellationToken cancellationToken)
        {
            var packageSources = await ServiceLocator.GetInstanceAsync<ISourceRepositoryProvider>();
            Assumes.NotNull(packageSources);
            packageSources.PackageSourceProvider.SavePackageSources(sources);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _authorizationServiceClient?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
