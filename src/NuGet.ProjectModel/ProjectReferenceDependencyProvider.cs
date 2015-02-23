// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace NuGet.ProjectModel
{
    public class ProjectReferenceDependencyProvider : IDependencyProvider
    {
        private readonly IProjectResolver _projectResolver;

        public ProjectReferenceDependencyProvider(IProjectResolver projectResolver)
        {
            _projectResolver = projectResolver;
        }

        public IEnumerable<string> GetAttemptedPaths(FrameworkName targetFramework)
        {
            return _projectResolver.SearchPaths.Select(p => Path.Combine(p, "{name}", "project.json"));
        }

        public bool SupportsType(string libraryType)
        {
            return string.IsNullOrEmpty(libraryType) ||
                   string.Equals(libraryType, LibraryTypes.Project);
        }

        public Library GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            string name = libraryRange.Name;

            PackageSpec project;

            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            // This never returns null
            var targetFrameworkInfo = project.GetTargetFramework(targetFramework);
            var targetFrameworkDependencies = targetFrameworkInfo.Dependencies;

            if (targetFramework.IsDesktop())
            {
                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "mscorlib",
                        Type = LibraryTypes.FrameworkOrGacAssembly
                    }
                });

                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "System",
                        Type = LibraryTypes.FrameworkOrGacAssembly
                    }
                });

                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "System.Core",
                        Type = LibraryTypes.FrameworkOrGacAssembly
                    }
                });

                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "Microsoft.CSharp",
                        Type = LibraryTypes.FrameworkOrGacAssembly
                    }
                });
            }

            var dependencies = project.Dependencies.Concat(targetFrameworkDependencies).ToList();

            // Mark the library as unresolved if there were specified frameworks
            // and none of them resolved
            bool unresolved = targetFrameworkInfo.FrameworkName == null &&
                              project.TargetFrameworks.Any();

            var description = new Library
            {
                LibraryRange = libraryRange,
                Identity = new LibraryIdentity
                {
                    Name = project.Name,
                    Version = project.Version,
                    Type = LibraryTypes.Project,
                },
                Path = project.FilePath,
                Dependencies = dependencies,
                Resolved = !unresolved
            };

            description.Items["project"] = project;

            return description;
        }
    }
}
