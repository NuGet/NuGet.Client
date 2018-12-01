// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands.Utility
{
    public class SolutionPackagesContentHashUtility : ISolutionPackagesContentHashUtility
    {
        private readonly SourceRepository _packagesFolderSourceRepository;
        private readonly IReadOnlyList<SourceRepository> _globalPackageFolderRepositories;
        private readonly ILogger _logger;

        public SolutionPackagesContentHashUtility(SourceRepository packagesFolderSourceRepository, IReadOnlyList<SourceRepository> globalPackageFolderRepositories, ILogger logger)
        {
            if (!packagesFolderSourceRepository.PackageSource.IsLocal)
            {
                throw new ArgumentException($"Internal error. Package source {packagesFolderSourceRepository.PackageSource.Name} is not local");
            }

            foreach (var repository in globalPackageFolderRepositories)
            {
                if (!repository.PackageSource.IsLocal)
                {
                    throw new ArgumentException($"Internal error. Package source {repository.PackageSource.Name} is not local");
                }
            }

            _packagesFolderSourceRepository = packagesFolderSourceRepository;
            _globalPackageFolderRepositories = globalPackageFolderRepositories;
            _logger = logger;
        }

        public async Task<string> GetContentHashAsync(PackageIdentity packageIdentity, CancellationToken token)
        {
            // try to read the .nupkg.metadata file from the solution packages folder
            var nupkgPath = GetNupkgPath(packageIdentity, _packagesFolderSourceRepository, token);
            var result = TryNupkgMetadata(nupkgPath);

            if (!result.Found)
            {
                var globalNupkgPath = GetNupkgPathInGlobalPackagesFolder(packageIdentity, token);
                if (globalNupkgPath != null)
                {
                    result = TryNupkgMetadata(globalNupkgPath);
                }
                else
                {
                    result = TrySignedPackageHash(nupkgPath, token);
                    if (!result.Found)
                    {
                        result = await TryArchiveHash(nupkgPath, token);
                    }
                }

                WriteNupkgMetadata(nupkgPath, result.ContentHash);
            }

            return result.ContentHash;
        }

        private string GetNupkgPath(PackageIdentity packageIdentity, SourceRepository source, CancellationToken token)
        {
            var findPackages = source.GetResource<FindLocalPackagesResource>();
            var package = findPackages.GetPackage(packageIdentity, _logger, token);
            return package?.Path;
        }

        private string GetNupkgPathInGlobalPackagesFolder(PackageIdentity packageIdentity, CancellationToken token)
        {
            foreach (var source in _globalPackageFolderRepositories)
            {
                var path = GetNupkgPath(packageIdentity, source, token);
                if (path != null)
                {
                    return path;
                }
            }

            return null;
        }

        private Result TryNupkgMetadata(string nupkgPath)
        {
            var metadataPath = Path.Combine(Path.GetDirectoryName(nupkgPath), PackagingCoreConstants.NupkgMetadataFileExtension);
            if (File.Exists(metadataPath))
            {
                var metadata = NupkgMetadataFileFormat.Read(metadataPath);
                return new Result(true, metadata.ContentHash);
            }

            return Result.NotFound;
        }

        private string GetNupkgMetadataPath(string nupkgPath)
        {
            return Path.Combine(Path.GetDirectoryName(nupkgPath), PackagingCoreConstants.NupkgMetadataFileExtension);
        }

        private Result TrySignedPackageHash(string filePath, CancellationToken token)
        {
            using (var reader = new PackageArchiveReader(filePath))
            {
                var hashString = reader.GetContentHashForSignedPackage(token);
                if (hashString != null)
                {
                    return new Result(true, hashString);
                }
            }

            return Result.NotFound;
        }

        private async Task<Result> TryArchiveHash(string filePath, CancellationToken token)
        {
            using (var reader = new PackageArchiveReader(filePath))
            {
                var hash = await reader.GetArchiveHashAsync(HashAlgorithmName.SHA512, token);
                var hashString = Convert.ToBase64String(hash);
                return new Result(true, hashString);
            }
        }

        private void WriteNupkgMetadata(string nupkgPath, string contentHash)
        {
            var metadataPath = GetNupkgMetadataPath(nupkgPath);

            var metadata = new NupkgMetadataFile()
            {
                ContentHash = contentHash
            };

            NupkgMetadataFileFormat.Write(metadataPath, metadata);
        }

        private struct Result
        {
            public static readonly Result NotFound = new Result(false, null);

            public Result(bool found, string contentHash)
            {
                Found = found;
                ContentHash = contentHash;
            }

            public bool Found { get; }
            public string ContentHash { get; }
        }
    }
}
