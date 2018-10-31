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
    public class ContentHashUtility : IContentHashUtility
    {
        private readonly ICollection<SourceRepository> _repositories;
        private readonly ILogger _logger;

        public ContentHashUtility(ICollection<SourceRepository> repositories, ILogger logger)
        {
            foreach(var repository in repositories)
            {
                if (!repository.PackageSource.IsLocal)
                {
                    throw new ArgumentException($"Internal error. Package source {repository.PackageSource.Name} is not local");
                }
            }

            _repositories = repositories;
            _logger = logger;
        }

        public async Task<string> GetContentHashAsync(PackageIdentity packageIdentity, CancellationToken token)
        {
            var filePaths = FindNupkgs(packageIdentity, token);

            var result = TryNupkgMetadata(filePaths);
            if (result.Found)
            {
                return result.ContentHash;
            }

            result = TrySignedPackageHash(filePaths, token);
            if (result.Found)
            {
                return result.ContentHash;
            }

            result = TryNupkgSha512(filePaths);
            if (result.Found)
            {
                return result.ContentHash;
            }

            result = await TryArchiveHash(filePaths, token);
            return result.ContentHash;
        }

        private List<string> FindNupkgs(PackageIdentity packageIdentity, CancellationToken token)
        {
            var filePaths = new List<string>();
            foreach (var source in _repositories)
            {
                var findPackages = source.GetResource<FindLocalPackagesResource>();
                var package = findPackages.GetPackage(packageIdentity, _logger, token);
                if (package != null)
                {
                    filePaths.Add(package.Path);
                }
            }

            return filePaths;
        }

        private Result TryNupkgMetadata(List<string> filePaths)
        {
            foreach (var nupkgPath in filePaths)
            {
                var metadataPath = Path.Combine(Path.GetDirectoryName(nupkgPath), PackagingCoreConstants.NupkgMetadataFileExtension);
                if (File.Exists(metadataPath))
                {
                    var metadata = NupkgMetadataFileFormat.Read(metadataPath);
                    return new Result(true, metadata.ContentHash);
                }
            }

            return Result.NotFound;
        }

        private Result TrySignedPackageHash(List<string> filePaths, CancellationToken token)
        {
            foreach (var filePath in filePaths)
            {
                using (var reader = new PackageArchiveReader(filePath))
                {
                    var hashString = reader.GetContentHashForSignedPackage(token);
                    if (hashString != null)
                    {
                        return new Result(true, hashString);
                    }
                }
            }

            return Result.NotFound;
        }

        private Result TryNupkgSha512(List<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                var hashFilePath = filePath + ".sha512";
                if (File.Exists(hashFilePath))
                {
                    var hashString = File.ReadAllText(hashFilePath);
                    return new Result(true, hashString);
                }
            }

            return Result.NotFound;
        }

        private async Task<Result> TryArchiveHash(List<string> filePaths, CancellationToken token)
        {
            foreach (var filePath in filePaths)
            {
                using (var reader = new PackageArchiveReader(filePath))
                {
                    var hash = await reader.GetArchiveHashAsync(HashAlgorithmName.SHA512, token);
                    var hashString = Convert.ToBase64String(hash);
                    return new Result(true, hashString);
                }
            }

            return Result.NotFound;
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
