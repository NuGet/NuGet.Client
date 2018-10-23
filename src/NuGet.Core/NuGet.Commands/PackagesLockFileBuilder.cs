// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Shared;

namespace NuGet.Commands
{
    public class PackagesLockFileBuilder
    {
        public PackagesLockFile CreateNuGetLockFile(LockFile assetsFile)
        {
            var lockFile = new PackagesLockFile();

            var libraryLookup = assetsFile.Libraries.Where(e => e.Type == LibraryType.Package)
                .ToDictionary(e => new PackageIdentity(e.Name, e.Version));

            foreach (var target in assetsFile.Targets)
            {
                var nuGettarget = new PackagesLockFileTarget()
                {
                    TargetFramework = target.TargetFramework,
                    RuntimeIdentifier = target.RuntimeIdentifier
                };

                var framework = assetsFile.PackageSpec.TargetFrameworks.FirstOrDefault(
                    f => EqualityUtility.EqualsWithNullCheck(f.FrameworkName, target.TargetFramework));

                var libraries = target.Libraries;

                // check if this is RID-based graph then only add those libraries which differ from original TFM.
                if (!string.IsNullOrEmpty(target.RuntimeIdentifier))
                {
                    var onlyTFM = assetsFile.Targets.First(t => EqualityUtility.EqualsWithNullCheck(t.TargetFramework, target.TargetFramework));

                    libraries = target.Libraries.Where(lib => !onlyTFM.Libraries.Any(tfmLib => tfmLib.Equals(lib))).ToList();
                }

                foreach (var library in libraries.Where(e => e.Type == LibraryType.Package))
                {
                    var identity = new PackageIdentity(library.Name, library.Version);

                    var dependency = new LockFileDependency()
                    {
                        Id = library.Name,
                        ResolvedVersion = library.Version,
                        ContentHash = libraryLookup[identity].Sha512,
                        Dependencies = library.Dependencies
                    };

                    var framework_dep = framework?.Dependencies.FirstOrDefault(
                        dep => StringComparer.OrdinalIgnoreCase.Equals(dep.Name, library.Name));

                    if (framework_dep != null)
                    {
                        dependency.Type = PackageDependencyType.Direct;
                        dependency.RequestedVersion = framework_dep.LibraryRange.VersionRange;
                    }
                    else
                    {
                        dependency.Type = PackageDependencyType.Transitive;
                    }

                    nuGettarget.Dependencies.Add(dependency);
                }

                foreach (var projectReference in libraries.Where(e => e.Type == LibraryType.Project || e.Type == LibraryType.ExternalProject))
                {
                    var dependency = new LockFileDependency()
                    {
                        Id = projectReference.Name,
                        Dependencies = projectReference.Dependencies,
                        Type = PackageDependencyType.Project
                    };

                    nuGettarget.Dependencies.Add(dependency);
                }

                nuGettarget.Dependencies = nuGettarget.Dependencies.OrderBy(d => d.Type).ToList();

                lockFile.Targets.Add(nuGettarget);
            }

            return lockFile;
        }

        public async Task<PackagesLockFile> CreateNuGetLockFileAsync(IEnumerable<PackageReference> installedPackages, Func<PackageIdentity, string> getNupkgFilePath, CancellationToken token)
        {
            Debug.Assert(installedPackages != null);
            Debug.Assert(getNupkgFilePath != null);

            var lockFile = new PackagesLockFile();

            foreach (var targetFramework in installedPackages.GroupBy(p => p.TargetFramework).OrderBy(g => g.Key))
            {
                var target = new PackagesLockFileTarget
                {
                    TargetFramework = targetFramework.Key,
                    Dependencies = new List<LockFileDependency>()
                };

                foreach (var package in targetFramework.OrderBy(p => p.PackageIdentity))
                {
                    string hash;

                    var file = getNupkgFilePath(package.PackageIdentity);
                    if (file == null)
                    {
                        throw new Exception("couldn't find package");
                    }

                    using (var reader = new PackageArchiveReader(file))
                    {
                        hash = reader.GetContentHashForSignedPackage(token);
                        if (hash == null)
                        {
                            hash = Convert.ToBase64String(await reader.GetArchiveHashAsync(HashAlgorithmName.SHA512, token));
                        }
                    }

                    var dependency = new LockFileDependency
                    {
                        Id = package.PackageIdentity.Id,
                        Type = PackageDependencyType.Direct,
                        RequestedVersion = package.AllowedVersions,
                        ResolvedVersion = package.PackageIdentity.Version,
                        ContentHash = hash
                    };

                    target.Dependencies.Add(dependency);
                }

                lockFile.Targets.Add(target);
            }

            return lockFile;
        }
    }
}
