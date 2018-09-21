// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Represents a provider interface capable of instantiating <see cref="NuGetProject"/>.
    /// </summary>
    public interface IProjectSystemProvider
    {
        /// <summary>
        /// Attempts to create a <see cref="NuGetProject"/> instance if the input DTE project
        /// matches certain criteria.
        /// </summary>
        /// <param name="project">Existing Visual Studio object.</param>
        /// <param name="context">Context used to create a new project instance.</param>
        /// <param name="result">New instance if instantiation succeeds, null otherwise.</param>
        /// <returns>True if operation has succeeded.</returns>
        bool TryCreateNuGetProject(EnvDTE.Project project, ProjectSystemProviderContext context, out NuGetProject result);
    }
}
