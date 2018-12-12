// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    internal class SolutionPackagesContentHashUtility : ISolutionPackagesContentHashUtility
    {
        private readonly FindLocalPackagesResource _packagesFolderFindPackagesResource;
        private readonly ILogger _logger;

        internal SolutionPackagesContentHashUtility(SourceRepository packagesFolderSourceRepository, ILogger logger)
        {
            _packagesFolderFindPackagesResource = packagesFolderSourceRepository.GetResource<FindLocalPackagesResource>();
            _logger = logger;
        }

        public async Task<string> GetContentHashAsync(PackageIdentity packageIdentity, CancellationToken token)
        {
            // try to read the .nupkg.metadata file from the solution packages folder
            var nupkgPath = GetNupkgPath(packageIdentity, token);
            var result = TryGetNupkgMetadata(nupkgPath);

            if (!result.Found)
            {
                result = TryGetSignedPackageHash(nupkgPath, token);
                if (!result.Found)
                {
                    var contentHash = await GetArchiveHash(nupkgPath, token);
                    result = new Result(true, contentHash);
                }

                WriteNupkgMetadata(nupkgPath, result.ContentHash);
            }

            return result.ContentHash;
        }

        private string GetNupkgPath(PackageIdentity packageIdentity, CancellationToken token)
        {
            var package = _packagesFolderFindPackagesResource.GetPackage(packageIdentity, _logger, token);
            return package?.Path;
        }

        private Result TryGetNupkgMetadata(string nupkgPath)
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

        private Result TryGetSignedPackageHash(string filePath, CancellationToken token)
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

        private async Task<string> GetArchiveHash(string filePath, CancellationToken token)
        {
            using (var reader = new PackageArchiveReader(filePath))
            {
                var hash = await reader.GetArchiveHashAsync(HashAlgorithmName.SHA512, token);
                var hashString = Convert.ToBase64String(hash);
                return hashString;
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
