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
    public sealed class NuGetProjectManagerService : INuGetProjectManagerService
    {
        private readonly ServiceActivationOptions _options;
        private readonly IServiceBroker _serviceBroker;
        private readonly AuthorizationServiceClient _authorizationServiceClient;

        public NuGetProjectManagerService(ServiceActivationOptions options, IServiceBroker sb, AuthorizationServiceClient ac)
        {
            _options = options;
            _serviceBroker = sb;
            _authorizationServiceClient = ac;
        }

        public async ValueTask<IReadOnlyCollection<PackageReference>> GetInstalledPackagesAsync(IReadOnlyCollection<string> projectGuids, CancellationToken ct)
        {
            var solutionManager = await ServiceLocator.GetInstanceAsync<IVsSolutionManager>();
            Assumes.NotNull(solutionManager);

            NuGetProject[]? projects = (await solutionManager.GetNuGetProjectsAsync())
                .Where(p => projectGuids.Contains(p.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId)))
                .ToArray();

            // Read package references from all projects.
            IEnumerable<Task<IEnumerable<PackageReference>>>? tasks = projects.Select(project => project.GetInstalledPackagesAsync(ct));
            IEnumerable<PackageReference>[]? packageReferences = await Task.WhenAll(tasks);

            return packageReferences.SelectMany(e => e).ToArray();
        }

        public async ValueTask<object> GetMetadataAsync(string projectGuid, string key, CancellationToken token)
        {
            var solutionManager = await ServiceLocator.GetInstanceAsync<IVsSolutionManager>();
            Assumes.NotNull(solutionManager);

            NuGetProject project = await GetNuGetProjectMatchingProjectGuidAsync(projectGuid);

            return project.GetMetadata<object>(key);
        }

        public async ValueTask<(bool, object)> TryGetMetadataAsync(string projectGuid, string key, CancellationToken token)
        {
            NuGetProject project = await GetNuGetProjectMatchingProjectGuidAsync(projectGuid);

            bool success = project.TryGetMetadata(key, out object value);
            return (success, value);
        }

        public async ValueTask<NuGetProjectKind> GetProjectKindAsync(string projectGuid, CancellationToken token)
        {
            NuGetProject project = await GetNuGetProjectMatchingProjectGuidAsync(projectGuid);
            return ProjectContextInfo.GetProjectKind(project);
        }

        public void Dispose()
        {
            _authorizationServiceClient.Dispose();
            GC.SuppressFinalize(this);
        }

        private async ValueTask<NuGetProject> GetNuGetProjectMatchingProjectGuidAsync(string projectGuid)
        {
            var solutionManager = await ServiceLocator.GetInstanceAsync<IVsSolutionManager>();
            Assumes.NotNull(solutionManager);

            NuGetProject project = (await solutionManager.GetNuGetProjectsAsync())
                .First(p => projectGuid.Equals(p.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId), StringComparison.OrdinalIgnoreCase));
            return project;
        }
    }
}
