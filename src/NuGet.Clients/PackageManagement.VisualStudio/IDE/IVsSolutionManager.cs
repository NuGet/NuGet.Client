﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Extends core <see cref="ISolutionManager"/> API with VisualStudio specific methods.
    /// </summary>
    public interface IVsSolutionManager : ISolutionManager
    {
        /// <summary>
        /// Retrieves <see cref="NuGetProject"/> instance associated with VS project.
        /// Creates new instance if not found in project system cache.
        /// </summary>
        /// <param name="project">VS project.</param>
        /// <param name="projectContext">Context object for new instance creation.</param>
        /// <returns>Existing or new <see cref="NuGetProject"/> instance.</returns>
        Task<NuGetProject> GetOrCreateProjectAsync(EnvDTE.Project project, INuGetProjectContext projectContext);
    }
}
