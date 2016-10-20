// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Represents the interface necessary for adding a project to a <see cref="DependencyGraphSpec"/>-based restore.
    /// This interface has logic for creating the <see cref="PackageSpec"/> as well as for detecting no-op cases where
    /// a restore is not necessary.
    /// </summary>
    public interface IDependencyGraphProject
    {
        /// <summary>
        /// Gets the path to the MSBuild project file. This is an absolute path.
        /// </summary>
        string MSBuildProjectPath { get; }

        /// <summary>
        /// Get the time when the project was last modified. This is used for cache invalidation.
        /// </summary>
        DateTimeOffset LastModified { get; }
        
        /// <summary>
        /// Project spec
        /// </summary>
        Task<PackageSpec> GetPackageSpecAsync(DependencyGraphCacheContext context);
        
        /// <summary>
        /// Full project closure and tools.
        /// </summary>
        Task<DependencyGraphSpec> GetDependencyGraphSpecAsync(DependencyGraphCacheContext context);

        /// <summary>
        /// Direct project references of the project.
        /// </summary>
        Task<IReadOnlyList<IDependencyGraphProject>> GetDirectProjectReferencesAsync(DependencyGraphCacheContext context);

        Task<bool> IsRestoreRequired(
            IEnumerable<VersionFolderPathResolver> pathResolvers,
            ISet<PackageIdentity> packagesChecked,
            DependencyGraphCacheContext context);
    }
}
