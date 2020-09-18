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
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public sealed class ProjectContextInfo : IProjectContextInfo
    {
        private const string LiveShareUriScheme = "vsls";
        private const string ProjectGuidQueryString = "projectGuid";

        public ProjectContextInfo(string projectId, ProjectStyle projectStyle, NuGetProjectKind projectKind)
        {
            ProjectId = projectId;
            ProjectStyle = projectStyle;
            ProjectKind = projectKind;
        }

        public string ProjectId { get; }
        public NuGetProjectKind ProjectKind { get; }
        public ProjectStyle ProjectStyle { get; }

        public async ValueTask<bool> IsUpgradeableAsync(CancellationToken cancellationToken)
        {
            IServiceBroker remoteBroker = await GetRemoteServiceBrokerAsync();

            using (INuGetProjectUpgraderService? projectUpgrader = await remoteBroker.GetProxyAsync<INuGetProjectUpgraderService>(
                NuGetServices.ProjectUpgraderService,
                cancellationToken: cancellationToken))
            {
                Assumes.NotNull(projectUpgrader);

                return await projectUpgrader.IsProjectUpgradeableAsync(ProjectId, cancellationToken);
            }
        }

        public async ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken)
        {
            using (INuGetProjectManagerService projectManager = await GetProjectManagerAsync(cancellationToken))
            {
                return await projectManager.GetInstalledPackagesAsync(new string[] { ProjectId }, cancellationToken);
            }
        }

        public async ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>> GetTransitivePackagesAsync(CancellationToken cancellationToken)
        {
            using (INuGetProjectManagerService projectManager = await GetProjectManagerAsync(cancellationToken))
            {
                return await projectManager.GetTransitivePackagesAsync(new string[] { ProjectId }, cancellationToken);
            }
        }

        public async ValueTask<IProjectMetadataContextInfo> GetMetadataAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (INuGetProjectManagerService projectManager = await GetProjectManagerAsync(cancellationToken))
            {
                return await projectManager.GetMetadataAsync(ProjectId, cancellationToken);
            }
        }

        public async ValueTask<string?> GetUniqueNameOrNameAsync(CancellationToken cancellationToken)
        {
            IProjectMetadataContextInfo metadata = await GetMetadataAsync(cancellationToken);

            return metadata.UniqueName ?? metadata.Name;
        }

        public async ValueTask<(bool, string?)> TryGetInstalledPackageFilePathAsync(
            PackageIdentity packageIdentity,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(packageIdentity);

            cancellationToken.ThrowIfCancellationRequested();

            using (INuGetProjectManagerService projectManager = await GetProjectManagerAsync(cancellationToken))
            {
                return await projectManager.TryGetInstalledPackageFilePathAsync(ProjectId, packageIdentity, cancellationToken);
            }
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
            using (INuGetProjectManagerService projectManager = await GetProjectManagerAsync(cancellationToken))
            {
                return await projectManager.GetProjectAsync(projectId, cancellationToken);
            }
        }

        private static NuGetProjectKind GetProjectKind(NuGetProject nugetProject)
        {
            // Order matters
            NuGetProjectKind projectKind = NuGetProjectKind.Unknown;
            if (nugetProject is BuildIntegratedNuGetProject)
            {
                projectKind = NuGetProjectKind.PackageReference;
            }
            else if (nugetProject is MSBuildNuGetProject)
            {
                projectKind = NuGetProjectKind.PackagesConfig;
            }
            else if (nugetProject is ProjectKNuGetProjectBase)
            {
                projectKind = NuGetProjectKind.ProjectK;
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

        private static async ValueTask<INuGetProjectManagerService> GetProjectManagerAsync(CancellationToken cancellationToken)
        {
            IServiceBroker serviceBroker = await GetRemoteServiceBrokerAsync();

#pragma warning disable ISB001 // Dispose of proxies
            INuGetProjectManagerService? projectManager = await serviceBroker.GetProxyAsync<INuGetProjectManagerService>(
                 NuGetServices.ProjectManagerService,
                 cancellationToken: cancellationToken);
#pragma warning restore ISB001 // Dispose of proxies

            Assumes.NotNull(projectManager);

            return projectManager;
        }

        private static async ValueTask<IServiceBroker> GetRemoteServiceBrokerAsync()
        {
            var serviceBrokerContainer = await AsyncServiceProvider.GlobalProvider.GetServiceAsync<SVsBrokeredServiceContainer, IBrokeredServiceContainer>();
            Assumes.NotNull(serviceBrokerContainer);
            return serviceBrokerContainer.GetFullAccessServiceBroker();
        }
    }
}
