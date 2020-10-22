// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using NuGet.Packaging.Core;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class IProjectContextInfoExtensions
    {
        public static async ValueTask<bool> IsUpgradeableAsync(
            this IProjectContextInfo projectContextInfo,
            IServiceBroker serviceBroker,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(projectContextInfo);
            Assumes.NotNull(serviceBroker);

            cancellationToken.ThrowIfCancellationRequested();

            using (INuGetProjectUpgraderService? projectUpgrader = await serviceBroker.GetProxyAsync<INuGetProjectUpgraderService>(
                NuGetServices.ProjectUpgraderService,
                cancellationToken: cancellationToken))
            {
                Assumes.NotNull(projectUpgrader);

                return await projectUpgrader.IsProjectUpgradeableAsync(projectContextInfo.ProjectId, cancellationToken);
            }
        }

        public static async ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>> GetInstalledPackagesAsync(
            this IProjectContextInfo projectContextInfo,
            IServiceBroker serviceBroker,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(projectContextInfo);
            Assumes.NotNull(serviceBroker);

            cancellationToken.ThrowIfCancellationRequested();

            using (INuGetProjectManagerService projectManager = await GetProjectManagerAsync(serviceBroker, cancellationToken))
            {
                return await projectManager.GetInstalledPackagesAsync(new string[] { projectContextInfo.ProjectId }, cancellationToken);
            }
        }

        public static async ValueTask<IProjectMetadataContextInfo> GetMetadataAsync(
            this IProjectContextInfo projectContextInfo,
            IServiceBroker serviceBroker,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(projectContextInfo);
            Assumes.NotNull(serviceBroker);

            cancellationToken.ThrowIfCancellationRequested();

            using (INuGetProjectManagerService projectManager = await GetProjectManagerAsync(serviceBroker, cancellationToken))
            {
                return await projectManager.GetMetadataAsync(projectContextInfo.ProjectId, cancellationToken);
            }
        }

        public static async ValueTask<string?> GetUniqueNameOrNameAsync(
            this IProjectContextInfo projectContextInfo,
            IServiceBroker serviceBroker,
            CancellationToken cancellationToken)
        {
            IProjectMetadataContextInfo metadata = await GetMetadataAsync(projectContextInfo, serviceBroker, cancellationToken);

            return metadata.UniqueName ?? metadata.Name;
        }

        public static async ValueTask<(bool, string?)> TryGetInstalledPackageFilePathAsync(
            this IProjectContextInfo projectContextInfo,
            IServiceBroker serviceBroker,
            PackageIdentity packageIdentity,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(projectContextInfo);
            Assumes.NotNull(serviceBroker);
            Assumes.NotNull(packageIdentity);

            cancellationToken.ThrowIfCancellationRequested();

            using (INuGetProjectManagerService projectManager = await GetProjectManagerAsync(serviceBroker, cancellationToken))
            {
                return await projectManager.TryGetInstalledPackageFilePathAsync(
                    projectContextInfo.ProjectId,
                    packageIdentity,
                    cancellationToken);
            }
        }

        private static async ValueTask<INuGetProjectManagerService> GetProjectManagerAsync(
            IServiceBroker serviceBroker,
            CancellationToken cancellationToken)
        {
#pragma warning disable ISB001 // Dispose of proxies
            INuGetProjectManagerService? projectManager = await serviceBroker.GetProxyAsync<INuGetProjectManagerService>(
                 NuGetServices.ProjectManagerService,
                 cancellationToken: cancellationToken);
#pragma warning restore ISB001 // Dispose of proxies

            Assumes.NotNull(projectManager);

            return projectManager;
        }
    }
}
