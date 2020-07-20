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
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    public class NuGetProjectManagerService : INuGetProjectManagerService
    {
        private bool _disposedValue;
        private readonly ServiceActivationOptions _options;
        private readonly IServiceBroker _serviceBroker;
        private readonly AuthorizationServiceClient _authorizationServiceClient;

        public NuGetProjectManagerService(ServiceActivationOptions options, IServiceBroker sb, AuthorizationServiceClient ac, CancellationToken ct)
        {
            _options = options;
            _serviceBroker = sb;
            _authorizationServiceClient = ac;
        }

        public async ValueTask<IReadOnlyCollection<PackageReference>> GetInstalledPackagesAsync(IReadOnlyCollection<string> projectGuids, CancellationToken ct)
        {
            var solutionManager = await ServiceLocator.GetInstanceAsync<IVsSolutionManager>();
            Assumes.NotNull(solutionManager);

            var projects = (await solutionManager.GetNuGetProjectsAsync()).Where(p => projectGuids.Contains(p.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId))).ToArray();

            // Read package references from all projects.
            var tasks = projects.Select(project => project.GetInstalledPackagesAsync(ct));
            var packageReferences = await Task.WhenAll(tasks);

            return packageReferences.SelectMany(e => e).ToArray();
        }

        public async ValueTask<object> GetMetadataAsync(string projectGuid, string key, CancellationToken token)
        {
            var solutionManager = await ServiceLocator.GetInstanceAsync<IVsSolutionManager>();
            Assumes.NotNull(solutionManager);

            var project = (await solutionManager.GetNuGetProjectsAsync()).First(p => projectGuid.Equals(p.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId), StringComparison.OrdinalIgnoreCase));
            return await project.GetMetadataAsync(key, token);
        }

        public async ValueTask<(bool, object)> TryGetMetadataAsync(string projectGuid, string key, CancellationToken token)
        {
            var solutionManager = await ServiceLocator.GetInstanceAsync<IVsSolutionManager>();
            Assumes.NotNull(solutionManager);

            var project = (await solutionManager.GetNuGetProjectsAsync()).First(p => projectGuid.Equals(p.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId), StringComparison.OrdinalIgnoreCase));
            (bool success, object value) = await project.TryGetMetadataAsync(key, token);
            return (success, value);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _authorizationServiceClient.Dispose();
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
