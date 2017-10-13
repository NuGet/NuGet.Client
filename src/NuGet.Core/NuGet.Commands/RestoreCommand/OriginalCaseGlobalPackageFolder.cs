// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Repositories;

namespace NuGet.Commands
{
    public class OriginalCaseGlobalPackageFolder
    {
        private readonly List<NuGetv3LocalRepository> _localRepositories;
        private readonly RestoreRequest _request;
        private readonly ToolPathResolver _toolPathResolver;
        private readonly VersionFolderPathResolver _pathResolver;

        public OriginalCaseGlobalPackageFolder(RestoreRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _request = request;

            _localRepositories = new List<NuGetv3LocalRepository>();
            _localRepositories.Add(request.DependencyProviders.GlobalPackages);
            _localRepositories.AddRange(request.DependencyProviders.FallbackPackageFolders);

            _pathResolver = new VersionFolderPathResolver(
                _request.PackagesDirectory,
                _request.IsLowercasePackagesDirectory);

            _toolPathResolver = new ToolPathResolver(
                _request.PackagesDirectory,
                _request.IsLowercasePackagesDirectory);
        }

        public async Task CopyPackagesToOriginalCaseAsync(IEnumerable<RestoreTargetGraph> graphs, CancellationToken token)
        {
            // Keep track of the packages we've already converted to original case.
            var converted = new HashSet<PackageIdentity>();

            // Iterate over every package node.
            foreach (var graph in graphs)
            {
                var packages = graph
                    .Flattened
                    .Select(graphItem => graphItem.Data.Match)
                    .Where(remoteMatch => remoteMatch.Library.Type == LibraryType.Package);

                foreach (var remoteMatch in packages)
                {
                    var identity = GetPackageIdentity(remoteMatch);
                    var hashPath = _pathResolver.GetHashPath(identity.Id, identity.Version);

                    // No need to re-install the same package identity more than once or if it is
                    // already installed.
                    if (!converted.Add(identity) || File.Exists(hashPath))
                    {
                        continue;
                    }

                    var originalCaseContext = GetPathContext(identity, isLowercase: _request.IsLowercasePackagesDirectory);
                    var localPackageFilePath = GetLocalPackageFilePath(remoteMatch);
                    var packageIdentity = new PackageIdentity(remoteMatch.Library.Name, remoteMatch.Library.Version);
                    IPackageDownloader packageDependency = null;

                    if (string.IsNullOrEmpty(localPackageFilePath))
                    {
                        packageDependency = await remoteMatch.Provider.GetPackageDownloaderAsync(
                            packageIdentity,
                            _request.CacheContext,
                            _request.Log,
                            token);
                    }
                    else
                    {
                        packageDependency = new LocalPackageArchiveDownloader(
                            localPackageFilePath,
                            packageIdentity,
                            _request.Log);
                    }

                    // Install the package.
                    using (packageDependency)
                    {
                        var installed = await PackageExtractor.InstallFromSourceAsync(
                            packageDependency,
                            originalCaseContext,
                            token);

                        if (installed)
                        {
                            _request.Log.LogMinimal(string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Log_ConvertedPackageToOriginalCase,
                                identity));
                        }
                    }
                }
            }
        }

        public void ConvertLockFileToOriginalCase(LockFile lockFile)
        {
            var packageLibraries = lockFile
                .Libraries
                .Where(library => library.Type == LibraryType.Package);

            foreach (var library in packageLibraries)
            {
                var path = _pathResolver.GetPackageDirectory(library.Name, library.Version);
                library.Path = PathUtility.GetPathWithForwardSlashes(path);
            }
        }

        private PackageExtractionV3Context GetPathContext(PackageIdentity packageIdentity, bool isLowercase)
        {
            var signedPackageVerifier = new SignedPackageVerifier(
                            SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                            SignedPackageVerifierSettings.Default);

            return new PackageExtractionV3Context(
                packageIdentity,
                _request.PackagesDirectory,
                isLowercase,
                _request.Log,
                _request.PackageSaveMode,
                _request.XmlDocFileSaveMode,
                signedPackageVerifier);
        }

        private static PackageIdentity GetPackageIdentity(RemoteMatch remoteMatch)
        {
            return new PackageIdentity(
                remoteMatch.Library.Name,
                remoteMatch.Library.Version);
        }

        private string GetLocalPackageFilePath(RemoteMatch remoteMatch)
        {
            var library = remoteMatch.Library;

            // Try to get the package from the local repositories first.
            var localPackage = NuGetv3LocalRepositoryUtility.GetPackage(
                _localRepositories,
                library.Name,
                library.Version);

            if (localPackage != null && File.Exists(localPackage.Package.ZipPath))
            {
                return localPackage.Package.ZipPath;
            }

            return null;
        }
    }
}