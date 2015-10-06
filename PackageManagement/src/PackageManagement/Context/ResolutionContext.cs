// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Resolver;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Resolution context such as DependencyBehavior, IncludePrerelease and so on
    /// </summary>
    public class ResolutionContext
    {
        /// <summary>
        /// Public constructor to create the resolution context
        /// </summary>
        public ResolutionContext()
        {
            DependencyBehavior = DependencyBehavior.Lowest;
            IncludePrerelease = false;
            IncludeUnlisted = true;
            VersionConstraints = VersionConstraints.None;
            GatherCache = new GatherCache();
        }

        /// <summary>
        /// Public constructor to create the resolution context
        /// </summary>
        public ResolutionContext(
            DependencyBehavior dependencyBehavior,
            bool includePrelease,
            bool includeUnlisted,
            VersionConstraints versionConstraints)
        {
            DependencyBehavior = dependencyBehavior;
            IncludePrerelease = includePrelease;
            IncludeUnlisted = includeUnlisted;
            VersionConstraints = versionConstraints;
            GatherCache = new GatherCache();
        }

        /// <summary>
        /// Determines the dependency behavior
        /// </summary>
        public DependencyBehavior DependencyBehavior { get; }

        /// <summary>
        /// Determines if prerelease may be included in the installation
        /// </summary>
        public bool IncludePrerelease { get; }

        /// <summary>
        /// Determines if unlisted packages may be included in installation
        /// </summary>
        public bool IncludeUnlisted { get; }

        /// <summary>
        /// Determines the containts that are placed on package update selection with respect to the installed packages
        /// </summary>
        public VersionConstraints VersionConstraints { get; }

        /// <summary>
        /// Gathe cache containing cached packages that can be used across operations.
        /// Ex: Update-Package updates all packages across all projects, GatherCache stores
        /// the gathered packages and re-uses them across all sub operations.
        /// This property is for internal use or testing only.
        /// </summary>
        public GatherCache GatherCache { get; }
    }
}
