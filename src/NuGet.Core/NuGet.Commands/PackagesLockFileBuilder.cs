// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.LibraryModel;
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

                    CentralPackageVersion centralPackageVersion = null;
                    var libraryVersionIsCentrallyDefined = framework?.CentralPackageVersions.TryGetValue(library.Name, out centralPackageVersion);

                    if (framework_dep != null)
                    {
                        dependency.Type = PackageDependencyType.Direct;
                        dependency.RequestedVersion = framework_dep.LibraryRange.VersionRange;
                    }
                    else if (libraryVersionIsCentrallyDefined.HasValue && libraryVersionIsCentrallyDefined.Value && centralPackageVersion != null)
                    {
                        dependency.Type = PackageDependencyType.CentralTransitive;
                        dependency.RequestedVersion = centralPackageVersion.VersionRange;
                    }
                    else
                    {
                        dependency.Type = PackageDependencyType.Transitive;
                    }

                    nuGettarget.Dependencies.Add(dependency);
                }

                var projectFullPaths = assetsFile.Libraries
                    .Where(l => l.Type == LibraryType.Project || l.Type == LibraryType.ExternalProject)
                    .ToDictionary(l => new PackageIdentity(l.Name, l.Version), l => l.MSBuildProject);

                foreach (var projectReference in libraries.Where(e => e.Type == LibraryType.Project || e.Type == LibraryType.ExternalProject))
                {
                    var projectIdentity = new PackageIdentity(projectReference.Name, projectReference.Version);
                    var projectFullPath = projectFullPaths[projectIdentity];
                    var id = PathUtility.GetStringComparerBasedOnOS().Equals(Path.GetFileNameWithoutExtension(projectFullPath), projectReference.Name)
                        ? projectReference.Name.ToLowerInvariant()
                        : projectReference.Name;

                    var dependency = new LockFileDependency()
                    {
                        Id = id,
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

    }
}
