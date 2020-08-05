// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    public class ProjectContextInfo
    {
        private const string LiveShareUriScheme = "vsls";
        private const string ProjectGuidQueryString = "projectGuid";
        private readonly Guid _projectUniqueId;

        private ProjectContextInfo(Guid projectUniqueId)
        {
            _projectUniqueId = projectUniqueId;
        }

        public ProjectStyle ProjectStyle { get; private set; } = ProjectStyle.Unknown;
        public NuGetProjectKind ProjectKind { get; private set; } = NuGetProjectKind.Unknown;

        public async ValueTask<bool> IsProjectUpgradeableAsync(CancellationToken cancellationToken)
        {
            var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: cancellationToken))
            {
                Assumes.NotNull(nugetProjectManagerService);
                return await nugetProjectManagerService.IsNuGetProjectUpgradeableAsync(_projectUniqueId.ToString(), cancellationToken);
            }
        }

        public async Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken cancellationToken)
        {
            var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: cancellationToken))
            {
                Assumes.NotNull(nugetProjectManagerService);

                return await nugetProjectManagerService.GetInstalledPackagesAsync(new string[] { _projectUniqueId.ToString() }, cancellationToken);
            }
        }

        public async ValueTask<(bool, T)> TryGetMetadataAsync<T>(string key, CancellationToken token)
        {
            var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: token))
            {
                Assumes.NotNull(nugetProjectManagerService);

                (bool success, object value) = await nugetProjectManagerService.TryGetMetadataAsync(_projectUniqueId.ToString(), key, token);
                return (success, (T)value);
            }
        }

        public async ValueTask<T> GetMetadataAsync<T>(string key, CancellationToken token)
        {
            var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: token))
            {
                Assumes.NotNull(nugetProjectManagerService);

                return (T)await nugetProjectManagerService.GetMetadataAsync(_projectUniqueId.ToString(), key, token);
            }
        }

        public async ValueTask<string> GetUniqueNameOrNameAsync()
        {
            (bool success, string value) = await TryGetMetadataAsync<string>(NuGetProjectMetadataKeys.UniqueName, CancellationToken.None);
            if (success)
            {
                return value;
            }

            // Unique name is not set, simply return the name
            return await GetMetadataAsync<string>(NuGetProjectMetadataKeys.Name, CancellationToken.None);
        }

        public static ValueTask<ProjectContextInfo> CreateAsync(NuGetProject nugetProject, CancellationToken cancellationToken)
        {
            Assumes.NotNull(nugetProject);
            if (!Guid.TryParse(nugetProject.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId), out Guid projectGuid))
            {
                throw new InvalidOperationException();
            }

            var projectContextInfo = new ProjectContextInfo(projectGuid)
            {
                ProjectKind = GetProjectKind(nugetProject),
                ProjectStyle = nugetProject.ProjectStyle
            };

            return new ValueTask<ProjectContextInfo>(projectContextInfo);
        }

        public static async ValueTask<ProjectContextInfo> CreateAsync(Guid projectGuid, CancellationToken cancellationToken)
        {
            var projectKind = NuGetProjectKind.Unknown;
            var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: cancellationToken))
            {
                Assumes.NotNull(nugetProjectManagerService);
                projectKind = await nugetProjectManagerService.GetProjectKindAsync(projectGuid.ToString(), cancellationToken);
            }

            var projectContextInfo = new ProjectContextInfo(projectGuid)
            {
                ProjectKind = projectKind
            };

            return projectContextInfo;
        }

        internal static NuGetProjectKind GetProjectKind(NuGetProject nugetProject)
        {
            // TODO: Should we be force checking server for this call? Should this live somewhere else to enforce it?
            NuGetProjectKind projectKind = NuGetProjectKind.Unknown;
            if (nugetProject is INuGetIntegratedProject)
            {
                projectKind = NuGetProjectKind.Classic;
            }
            else if (nugetProject is ProjectKNuGetProjectBase)
            {
                projectKind = NuGetProjectKind.ProjectK;
            }
            else if (nugetProject is MSBuildNuGetProject)
            {
                projectKind = NuGetProjectKind.MSBuild;
            }
            else if (nugetProject is BuildIntegratedNuGetProject)
            {
                projectKind = NuGetProjectKind.BuildIntegrated;
            }

            return projectKind;
        }

        public static string GetProjectGuidStringFromVslsQueryString(string queryString)
        {
            if (Uri.TryCreate(queryString, UriKind.Absolute, out Uri pathUri))
            {
                if (string.Equals(pathUri.Scheme, LiveShareUriScheme, StringComparison.OrdinalIgnoreCase))
                {
                    Dictionary<string, string>? queryStrings = ParseQueryString(pathUri);
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
    }
}
