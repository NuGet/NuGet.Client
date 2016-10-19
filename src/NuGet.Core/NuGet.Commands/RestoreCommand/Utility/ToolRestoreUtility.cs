// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public static class ToolRestoreUtility
    {
        /// <summary>
        /// Build a package spec in memory to execute the tool restore as if it were
        /// its own project. For now, we always restore for a null runtime and a single
        /// constant framework.
        /// </summary>
        public static PackageSpec GetSpec(string projectFilePath, string id, VersionRange versionRange, NuGetFramework framework)
        {
            var name = $"{id}-{Guid.NewGuid().ToString()}";

            return new PackageSpec(new JObject())
            {
                Name = name, // make sure this package never collides with a dependency
                FilePath = projectFilePath,
                Dependencies = new List<LibraryDependency>(),
                Tools = new List<ToolDependency>(),
                TargetFrameworks =
                {
                    new TargetFrameworkInformation
                    {
                        FrameworkName = framework,
                        Dependencies = new List<LibraryDependency>
                        {
                            new LibraryDependency
                            {
                                LibraryRange = new LibraryRange(id, versionRange, LibraryDependencyTarget.Package)
                            }
                        }
                    }
                },
                RestoreMetadata = new ProjectRestoreMetadata()
                {
                    OutputType = RestoreOutputType.DotnetCliTool,
                    ProjectName = name,
                    ProjectUniqueName = name,
                    ProjectPath = projectFilePath
                }
            };
        }

        /// <summary>
        /// Returns the name of the single dependency in the spec or null.
        /// </summary>
        public static string GetToolIdOrNullFromSpec(PackageSpec spec)
        {
            return GetToolDependencyOrNullFromSpec(spec)?.Name;
        }

        /// <summary>
        /// Returns the name of the single dependency in the spec or null.
        /// </summary>
        public static LibraryDependency GetToolDependencyOrNullFromSpec(PackageSpec spec)
        {
            if (spec == null)
            {
                return null;
            }

            return spec.Dependencies.Concat(spec.TargetFrameworks.SelectMany(e => e.Dependencies)).SingleOrDefault();
        }

        public static LockFileTargetLibrary GetToolTargetLibrary(LockFile toolLockFile, string toolId)
        {
            var target = toolLockFile.Targets.Single();
            return target
                .Libraries
                .FirstOrDefault(l => StringComparer.OrdinalIgnoreCase.Equals(toolId, l.Name));
        }
    }
}