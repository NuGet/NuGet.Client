// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio.RemoteTypes
{
    [Export(typeof(IRemoteSourceRepositoryProvider))]
    internal sealed class RemoteSourceRepositoryProvider : IRemoteSourceRepositoryProvider
    {
        public IPackageSourceProvider PackageSourceProvider => new RemotePackageSourceProvider();

        public SourceRepository CreateRepository(PackageSource source)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBroker();
                var nuGetSettingsService = await remoteBroker.GetProxyAsync<INuGetSourceRepositoryService>(NuGetBrokeredServices.SourceRepositoryProviderService);
                using (nuGetSettingsService as IDisposable)
                {
                    return await nuGetSettingsService.CreateRepositoryAsync(source, CancellationToken.None);
                }
            });
        }

        public SourceRepository CreateRepository(PackageSource source, FeedType type)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBroker();
                var nuGetSettingsService = await remoteBroker.GetProxyAsync<INuGetSourceRepositoryService>(NuGetBrokeredServices.SourceRepositoryProviderService);
                using (nuGetSettingsService as IDisposable)
                {
                    return await nuGetSettingsService.CreateRepositoryAsync(source, type, CancellationToken.None);
                }
            });
        }

        public IEnumerable<SourceRepository> GetRepositories()
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBroker();
                var nuGetSettingsService = await remoteBroker.GetProxyAsync<INuGetSourceRepositoryService>(NuGetBrokeredServices.SourceRepositoryProviderService);
                using (nuGetSettingsService as IDisposable)
                {
                    return await nuGetSettingsService.GetRepositoriesAsync(CancellationToken.None);
                }
            });
        }
    }
}
