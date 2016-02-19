// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    public class PackageSearchResourceLocal : PackageSearchResource
    {
        private IPackageRepository V2Client { get; }

        public PackageSearchResourceLocal(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public PackageSearchResourceLocal(IPackageRepository repo)
        {
            V2Client = repo;
        }

        public async override Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchTerm, SearchFilter filters, int skip, int take, Logging.ILogger log, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                // Check if source is available.
                if (!IsHttpSource(V2Client.Source) && !IsLocalOrUNC(V2Client.Source))
                {
                    throw new InvalidOperationException(
                        Strings.FormatProtocol_Search_LocalSourceNotFound(V2Client.Source));
                }

                var query = V2Client.Search(
                    searchTerm,
                    filters.SupportedFrameworks,
                    filters.IncludePrerelease);

                // V2 sometimes requires that we also use an OData filter for
                // latest /latest prerelease version
                if (filters.IncludePrerelease)
                {
                    query = query.Where(p => p.IsAbsoluteLatestVersion);
                }
                else
                {
                    query = query.Where(p => p.IsLatestVersion);
                }
                query = query
                    .OrderByDescending(p => p.DownloadCount)
                    .ThenBy(p => p.Id);

                // Some V2 sources, e.g. NuGet.Server, local repository, the result contains all
                // versions of each package. So we need to group the result by Id.
                var collapsedQuery = query.AsEnumerable().AsCollapsed();

                // execute the query
                var packages = collapsedQuery
                    .Skip(skip)
                    .Take(take)
                    .ToArray();

                return packages
                    .Select(package => CreatePackageSearchResult(package, filters, cancellationToken))
                    .ToArray();
            });
        }

        private IPackageSearchMetadata CreatePackageSearchResult(IPackage package, SearchFilter filter, CancellationToken cancellationToken)
        {
            var metadata = new PackageSearchMetadata(package);
            return metadata
                .WithVersions(() => GetVersions(package, filter, CancellationToken.None));
        }

        public IEnumerable<VersionInfo> GetVersions(IPackage package, SearchFilter filter, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // apply the filters to the version list returned
            var packages = V2Client.FindPackagesById(package.Id)
                .Where(p => filter.IncludeDelisted || !p.Published.HasValue || p.Published.Value.Year > 1901)
                .Where(v => filter.IncludePrerelease || string.IsNullOrEmpty(v.Version.SpecialVersion))
                .ToArray();

            IEnumerable<VersionInfo> versions = packages
                .Select(p => new VersionInfo(V2Utilities.SafeToNuGetVer(p.Version), p.DownloadCount))
                .OrderByDescending(v => v.Version, VersionComparer.VersionRelease);

            var packageVersion = V2Utilities.SafeToNuGetVer(package.Version);
            if (!versions.Any(v => v.Version == packageVersion))
            {
                versions = versions.Concat(
                    new[] { new VersionInfo(packageVersion, package.DownloadCount) });
            }

            return versions;
        }

        private static bool IsHttpSource(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            Uri uri;
            if (Uri.TryCreate(source, UriKind.Absolute, out uri))
            {
                return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            }
            else
            {
                return false;
            }
        }

        private static bool IsLocalOrUNC(string currentSource)
        {
            Uri currentURI;
            if (Uri.TryCreate(currentSource, UriKind.RelativeOrAbsolute, out currentURI))
            {
                if (currentURI.IsFile || currentURI.IsUnc)
                {
                    if (Directory.Exists(currentSource))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}