// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.LibraryModel;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Helpers for dealing with xproj
    /// </summary>
    public static class XProjUtility
    {
        public static readonly string XProjExtension = ".xproj";

        /// <summary>
        /// Returns false for xproj and empty paths.
        /// </summary>
        public static bool IsMSBuildBasedProject(string projectPath)
        {
            return !string.IsNullOrEmpty(projectPath)
                && !projectPath.EndsWith(XProjExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the path to all referenced xprojs by using the package spec resolver.
        /// This will return references for ALL TxMs. Filtering based on the nearest TxM
        /// is needed to apply these results.
        /// </summary>
        /// <param name="filePath">Full path to the .xproj file.</param>
        public static List<string> GetProjectReferences(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            var output = new List<string>();

            if (filePath.EndsWith(XProjExtension, StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(filePath);
                var jsonPath = Path.Combine(dir, PackageSpec.PackageSpecFileName);

                // Ignore invalid projects with no project.json
                if (File.Exists(jsonPath))
                {
                    var projectName = Path.GetFileNameWithoutExtension(filePath);
                    var spec = JsonPackageSpecReader.GetPackageSpec(projectName, jsonPath);

                    var resolver = new PackageSpecResolver(spec);

                    // combine all dependencies
                    // This will include references for every TxM, these will have to be filtered later
                    var dependencies = new HashSet<LibraryDependency>();
                    dependencies.UnionWith(spec.Dependencies.Where(d => IsProjectReference(d)));
                    dependencies.UnionWith(spec.TargetFrameworks
                        .SelectMany(f => f.Dependencies)
                        .Where(d => IsProjectReference(d)));

                    // Attempt to look up each dependency
                    foreach (var dependency in dependencies)
                    {
                        PackageSpec childSpec;
                        if (resolver.TryResolvePackageSpec(dependency.Name, out childSpec))
                        {
                            var fileInfo = new FileInfo(childSpec.FilePath);

                            // dir/ProjectName.xproj
                            var xprojPath = Path.Combine(
                                fileInfo.DirectoryName,
                                fileInfo.Directory.Name + XProjExtension);

                            output.Add(xprojPath);
                        }
                    }
                }
            }

            return output;
        }

        private static bool IsProjectReference(LibraryDependency dependency)
        {
            var libraryType = dependency.LibraryRange.TypeConstraint;

            return (libraryType & (LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject)) != LibraryDependencyTarget.None;
        }
    }
}
