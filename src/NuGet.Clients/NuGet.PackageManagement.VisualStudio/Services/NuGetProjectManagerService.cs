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
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    public sealed class NuGetProjectManagerService : INuGetProjectManagerService
    {
        private readonly ServiceActivationOptions _options;
        private readonly IServiceBroker _serviceBroker;
        private readonly AuthorizationServiceClient _authorizationServiceClient;
        private readonly ISharedServiceState _state;

        public NuGetProjectManagerService(
            ServiceActivationOptions options,
            IServiceBroker serviceBroker,
            AuthorizationServiceClient authorizationServiceClient,
            ISharedServiceState state)
        {
            Assumes.NotNull(serviceBroker);
            Assumes.NotNull(authorizationServiceClient);
            Assumes.NotNull(state);

            _options = options;
            _serviceBroker = serviceBroker;
            _authorizationServiceClient = authorizationServiceClient;
            _state = state;
        }

        public void Dispose()
        {
            _authorizationServiceClient.Dispose();
            GC.SuppressFinalize(this);
        }

        public async ValueTask<IReadOnlyCollection<IProjectContextInfo>> GetProjectsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IVsSolutionManager? solutionManager = await _state.SolutionManager.GetValueAsync(cancellationToken);
            Assumes.NotNull(solutionManager);

            NuGetProject[] projects = (await solutionManager.GetNuGetProjectsAsync()).ToArray();
            var projectContexts = new List<IProjectContextInfo>(projects.Length);

            foreach (NuGetProject nugetProject in projects)
            {
                IProjectContextInfo? projectContext = await ProjectContextInfo.CreateAsync(nugetProject, cancellationToken);

                projectContexts.Add(projectContext);
            }

            return projectContexts;
        }

        public async ValueTask<IProjectContextInfo> GetProjectAsync(string projectId, CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(projectId);

            cancellationToken.ThrowIfCancellationRequested();

            NuGetProject? project = await SolutionUtility.GetNuGetProjectAsync(
                _state.SolutionManager,
                projectId,
                cancellationToken);

            Assumes.NotNull(project);

            return await ProjectContextInfo.CreateAsync(project, cancellationToken);
        }

        public async ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>> GetInstalledPackagesAsync(
            IReadOnlyCollection<string> projectIds,
            CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(projectIds);

            cancellationToken.ThrowIfCancellationRequested();

            IVsSolutionManager? solutionManager = await _state.SolutionManager.GetValueAsync(cancellationToken);
            Assumes.NotNull(solutionManager);

            NuGetProject[]? projects = (await solutionManager.GetNuGetProjectsAsync())
                .Where(p => projectIds.Contains(p.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId)))
                .ToArray();

            // Read package references from all projects.
            IEnumerable<Task<IEnumerable<PackageReference>>> tasks = projects.Select(project => project.GetInstalledPackagesAsync(cancellationToken));
            IEnumerable<PackageReference>[] packageReferences = await Task.WhenAll(tasks);

            return packageReferences.SelectMany(e => e).Select(pr => PackageReferenceContextInfo.Create(pr)).ToArray();
        }

        public async ValueTask<IReadOnlyCollection<PackageDependencyInfo>> GetInstalledPackagesDependencyInfoAsync(
            string projectId,
            bool includeUnresolved,
            CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(projectId);

            cancellationToken.ThrowIfCancellationRequested();

            NuGetPackageManager? packageManager = await _state.PackageManager.GetValueAsync(cancellationToken);
            Assumes.NotNull(packageManager);

            NuGetProject? project = await SolutionUtility.GetNuGetProjectAsync(
                _state.SolutionManager,
                projectId,
                cancellationToken);

            Assumes.NotNull(project);

            IEnumerable<PackageDependencyInfo>? results = await packageManager.GetInstalledPackagesDependencyInfo(
                project,
                cancellationToken,
                includeUnresolved);

            if (results == null)
            {
                return Array.Empty<PackageDependencyInfo>();
            }

            return results.ToArray();
        }

        public async ValueTask<object> GetMetadataAsync(string projectId, string key, CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(projectId);
            Assumes.NotNullOrEmpty(key);

            cancellationToken.ThrowIfCancellationRequested();

            NuGetProject? project = await SolutionUtility.GetNuGetProjectAsync(
                _state.SolutionManager,
                projectId,
                cancellationToken);

            Assumes.NotNull(project);

            return project.GetMetadata<object>(key);
        }

        public async ValueTask<(bool, object)> TryGetMetadataAsync(string projectId, string key, CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(projectId);
            Assumes.NotNullOrEmpty(key);

            cancellationToken.ThrowIfCancellationRequested();

            NuGetProject? project = await SolutionUtility.GetNuGetProjectAsync(
                _state.SolutionManager,
                projectId,
                cancellationToken);

            Assumes.NotNull(project);

            bool success = project.TryGetMetadata(key, out object value);

            return (success, value);
        }

        public async ValueTask<(bool, string?)> TryGetInstalledPackageFilePathAsync(
            string projectId,
            PackageIdentity packageIdentity,
            CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(projectId);
            Assumes.NotNull(packageIdentity);

            cancellationToken.ThrowIfCancellationRequested();

            NuGetProject? project = await SolutionUtility.GetNuGetProjectAsync(
                _state.SolutionManager,
                projectId,
                cancellationToken);

            Assumes.NotNull(project);

            string? packageFilePath = null;

            if (project is MSBuildNuGetProject msBuildProject)
            {
                packageFilePath = msBuildProject.FolderNuGetProject.GetInstalledPackageFilePath(packageIdentity);
            }

            bool success = packageFilePath != null;

            return (success, packageFilePath);
        }
    }
}
