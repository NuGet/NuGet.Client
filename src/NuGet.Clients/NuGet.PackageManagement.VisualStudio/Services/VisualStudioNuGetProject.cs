// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VisualStudioNuGetProject : NuGetProject
    {
        private const string LiveShareUriScheme = "vsls";
        private const string ProjectGuidQueryString = "projectGuid";
        private readonly string _projectGuidString;

        public VisualStudioNuGetProject(Guid projectGuid)
        {
            _projectGuidString = projectGuid.ToString();
        }

        public async override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: token))
            {
                Assumes.NotNull(nugetProjectManagerService);

                return await nugetProjectManagerService.GetInstalledPackagesAsync(new string[] { _projectGuidString }, token);
            }
        }

        public override Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, DownloadResourceResult downloadResourceResult, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public async override Task<(bool, object)> TryGetMetadataAsync(string key, CancellationToken token)
        {
            var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: token))
            {
                Assumes.NotNull(nugetProjectManagerService);

                return await nugetProjectManagerService.TryGetMetadataAsync(_projectGuidString, key, token);
            }
        }

        public async override Task<object> GetMetadataAsync(string key, CancellationToken token)
        {
            var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
            using (var nugetProjectManagerService = await remoteBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, cancellationToken: token))
            {
                Assumes.NotNull(nugetProjectManagerService);

                return await nugetProjectManagerService.GetMetadataAsync(_projectGuidString, key, token);
            }
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
