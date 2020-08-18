// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public sealed class ProjectContextInfo : IProjectContextInfo
    {
        private const string LiveShareUriScheme = "vsls";
        private const string ProjectGuidQueryString = "projectGuid";

        public ProjectContextInfo(string projectUniqueId, ProjectStyle projectStyle, NuGetProjectKind projectKind)
        {
            UniqueId = projectUniqueId;
            ProjectStyle = projectStyle;
            ProjectKind = projectKind;
        }

        public string UniqueId { get; }
        public NuGetProjectKind ProjectKind { get; }
        public ProjectStyle ProjectStyle { get; }

        public async ValueTask<bool> IsUpgradeableAsync(CancellationToken cancellationToken)
        {
            IServiceBroker remoteBroker = await GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: cancellationToken))
            {
                Assumes.NotNull(nugetProjectManagerService);
                return await nugetProjectManagerService.IsProjectUpgradeableAsync(UniqueId, cancellationToken);
            }
        }

        public async Task<IEnumerable<IPackageReferenceContextInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken)
        {
            IServiceBroker remoteBroker = await GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: cancellationToken))
            {
                Assumes.NotNull(nugetProjectManagerService);

                return await nugetProjectManagerService.GetInstalledPackagesAsync(new string[] { UniqueId }, cancellationToken);
            }
        }

        public async ValueTask<(bool, T)> TryGetMetadataAsync<T>(string key, CancellationToken cancellationToken)
        {
            IServiceBroker remoteBroker = await GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: cancellationToken))
            {
                Assumes.NotNull(nugetProjectManagerService);

                (bool success, object value) = await nugetProjectManagerService.TryGetMetadataAsync(UniqueId, key, cancellationToken);
                return (success, (T)value);
            }
        }

        public async ValueTask<T> GetMetadataAsync<T>(string key, CancellationToken cancellationToken)
        {
            IServiceBroker remoteBroker = await GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: cancellationToken))
            {
                Assumes.NotNull(nugetProjectManagerService);

                return (T)await nugetProjectManagerService.GetMetadataAsync(UniqueId, key, cancellationToken);
            }
        }

        public async ValueTask<string> GetUniqueNameOrNameAsync(CancellationToken cancellationToken)
        {
            (bool success, string value) = await TryGetMetadataAsync<string>(NuGetProjectMetadataKeys.UniqueName, cancellationToken);
            if (success)
            {
                return value;
            }

            // Unique name is not set, simply return the name
            return await GetMetadataAsync<string>(NuGetProjectMetadataKeys.Name, cancellationToken);
        }

        public static ValueTask<IProjectContextInfo> CreateAsync(NuGetProject nugetProject, CancellationToken cancellationToken)
        {
            Assumes.NotNull(nugetProject);
            if (!nugetProject.TryGetMetadata(NuGetProjectMetadataKeys.ProjectId, out string projectId))
            {
                throw new InvalidOperationException();
            }

            NuGetProjectKind projectKind = GetProjectKind(nugetProject);
            ProjectStyle projectStyle = nugetProject.ProjectStyle;
            return new ValueTask<IProjectContextInfo>(new ProjectContextInfo(projectId, projectStyle, projectKind));
        }

        public static async ValueTask<IProjectContextInfo> CreateAsync(string projectId, CancellationToken cancellationToken)
        {
            IProjectContextInfo projectContextInfo;
            IServiceBroker remoteBroker = await GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: cancellationToken))
            {
                Assumes.NotNull(nugetProjectManagerService);
                projectContextInfo = await nugetProjectManagerService.GetProjectAsync(projectId, cancellationToken);
            }

            return projectContextInfo;
        }

        internal static NuGetProjectKind GetProjectKind(NuGetProject nugetProject)
        {
            // Order matters
            NuGetProjectKind projectKind = NuGetProjectKind.Unknown;
            if (nugetProject is BuildIntegratedNuGetProject)
            {
                projectKind = NuGetProjectKind.BuildIntegrated;
            }
            else if (nugetProject is ProjectKNuGetProjectBase)
            {
                projectKind = NuGetProjectKind.ProjectK;
            }
            else if (nugetProject is MSBuildNuGetProject)
            {
                projectKind = NuGetProjectKind.MSBuild;
            }
            else if (nugetProject is INuGetIntegratedProject)
            {
                projectKind = NuGetProjectKind.Classic;
            }

            return projectKind;
        }

        public static string GetProjectGuidStringFromVslsQueryString(string queryString)
        {
            if (Uri.TryCreate(queryString, UriKind.Absolute, out Uri pathUri))
            {
                if (string.Equals(pathUri.Scheme, LiveShareUriScheme, StringComparison.OrdinalIgnoreCase))
                {
                    Dictionary<string, string> queryStrings = ParseQueryString(pathUri);
                    if (queryStrings.TryGetValue(ProjectGuidQueryString, out var projectGuid) && Guid.TryParse(projectGuid, out Guid result))
                    {
                        return result.ToString();
                    }
                }
            }

            return Guid.Empty.ToString();
        }

        private static Dictionary<string, string> ParseQueryString(Uri uri)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string queryString = uri.Query;

            if (queryString.StartsWith("?", StringComparison.Ordinal))
            {
                queryString = queryString.Substring(1);
            }

            if (queryString.Length == 0)
            {
                return result;
            }

            var queries = queryString.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var query in queries)
            {
                var nameValue = query.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (nameValue.Length == 2)
                {
                    result.Add(nameValue[0], nameValue[1]);
                }
            }

            return result;
        }

        private static async ValueTask<IServiceBroker> GetRemoteServiceBrokerAsync()
        {
            var serviceBrokerContainer = await AsyncServiceProvider.GlobalProvider.GetServiceAsync<SVsBrokeredServiceContainer, IBrokeredServiceContainer>();
            Assumes.NotNull(serviceBrokerContainer);
            return serviceBrokerContainer.GetFullAccessServiceBroker();
        }
    }
}
