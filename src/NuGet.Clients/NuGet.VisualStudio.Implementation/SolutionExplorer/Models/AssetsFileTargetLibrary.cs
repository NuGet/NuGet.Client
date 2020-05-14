// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NuGet.ProjectModel;

namespace NuGet.VisualStudio.SolutionExplorer.Models
{
    /// <summary>
    /// Data about a library (package/project) in a given target, from <c>project.assets.json</c>. Immutable.
    /// </summary>
    internal sealed class AssetsFileTargetLibrary
    {
        public static bool TryCreate(LockFileTargetLibrary lockFileLibrary, [NotNullWhen(returnValue: true)] out AssetsFileTargetLibrary? library)
        {
            AssetsFileLibraryType type;
            if (lockFileLibrary.Type == "package")
            {
                type = AssetsFileLibraryType.Package;
            }
            else if (lockFileLibrary.Type == "project")
            {
                type = AssetsFileLibraryType.Project;
            }
            else
            {
                library = null;
                return false;
            }

            library = new AssetsFileTargetLibrary(lockFileLibrary, type);
            return true;
        }

        private AssetsFileTargetLibrary(LockFileTargetLibrary library, AssetsFileLibraryType type)
        {
            Name = library.Name;
            Version = library.Version.ToNormalizedString();
            Type = type;

            // TODO use each dependency's version range in caption (won't have parity with top-level item unless we update caption or change SDK to return this information)
            // TODO use each dependency's include/exclude in browse object (won't have parity with top-level item until we rethink browse objects for them)
            Dependencies = library.Dependencies.Select(dep => dep.Id).ToImmutableArray();

            CompileTimeAssemblies = library.CompileTimeAssemblies
                .Select(a => a.Path)
                .Where(path => path != null)
                .Where(path => !IsPlaceholderFile(path))
                .ToImmutableArray(); // TODO do we want to use the 'properties' here? maybe for browse object

            FrameworkAssemblies = library.FrameworkAssemblies.ToImmutableArray();

            // TODO filter by code language as well (requires knowing project language): https://github.com/dotnet/NuGet.BuildTasks/blob/5244c490a425353ac12445567d87d674ae118836/src/Microsoft.NuGet.Build.Tasks/ResolveNuGetPackageAssets.cs#L572-L575
            ContentFiles = library.ContentFiles
                .Where(file => !IsPlaceholderFile(file.Path))
                .Select(file => new AssetsFileTargetLibraryContentFile(file))
                .ToImmutableArray();

            return;

            static bool IsPlaceholderFile(string path)
            {
                if (path.EndsWith("_._", StringComparison.Ordinal))
                {
                    if (path.Length == 3)
                    {
                        return true;
                    }

                    char separator = path[path.Length - 4];
                    return separator == '\\' || separator == '/';
                }

                return false;
            }
        }

        public string Name { get; }
        public string Version { get; }
        public AssetsFileLibraryType Type { get; }
        public ImmutableArray<string> Dependencies { get; }
        public ImmutableArray<string> FrameworkAssemblies { get; }
        public ImmutableArray<string> CompileTimeAssemblies { get; }
        public ImmutableArray<AssetsFileTargetLibraryContentFile> ContentFiles { get; }

        public override string ToString() => $"{Type} {Name} ({Version}) {Dependencies.Length} {(Dependencies.Length == 1 ? "dependency" : "dependencies")}";
    }
}
