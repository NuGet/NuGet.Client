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
    public sealed class NuGetSourcesService : INuGetSourcesService
    {
        private readonly ServiceActivationOptions _options;
        private readonly IServiceBroker _serviceBroker;
        private readonly AuthorizationServiceClient _authorizationServiceClient;

        public NuGetSourcesService(ServiceActivationOptions options, IServiceBroker serviceBroker, AuthorizationServiceClient authorizationServiceClient)
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

        public async ValueTask SavePackageSourcesAsync(IReadOnlyList<PackageSource> sources, PackageSourceUpdateOptions packageSourceUpdateOptions, CancellationToken cancellationToken)
        {
            var packageSources = await ServiceLocator.GetInstanceAsync<ISourceRepositoryProvider>();
            Assumes.NotNull(packageSources);

            var packageSources2 = packageSources.PackageSourceProvider as IPackageSourceProvider2;
            if (packageSources2 != null)
            {
                packageSources2.SavePackageSources(sources, packageSourceUpdateOptions);
            }
            else
            {
                packageSources.PackageSourceProvider.SavePackageSources(sources);
            }
        }

        public void Dispose()
        {
            _authorizationServiceClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
