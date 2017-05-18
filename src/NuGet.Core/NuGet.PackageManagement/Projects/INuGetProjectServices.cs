﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Provides an API to a collection of <see cref="NuGetProject"/> scoped services, such as
    /// - project references
    /// - assembly references
    /// - project capabilities
    /// - binding redirects
    /// - script executor
    /// </summary>
    public interface INuGetProjectServices
    {
        /// <summary>
        /// Service to access project's build properties.
        /// </summary>
        IProjectBuildProperties BuildProperties { get; }

        /// <summary>
        /// Service to query project system capabilities.
        /// </summary>
        IProjectSystemCapabilities Capabilities { get; }

        /// <summary>
        /// Service providing read-only access to references.
        /// </summary>
        IProjectSystemReferencesReader ReferencesReader { get; }

        /// <summary>
        /// Service to control references.
        /// </summary>
        IProjectSystemReferencesService References { get; }

        /// <summary>
        /// Service providing project system generic functionality.
        /// </summary>
        IProjectSystemService ProjectSystem { get; }

        /// <summary>
        /// Service to execute package scripts.
        /// </summary>
        IProjectScriptHostService ScriptService { get; }

        /// <summary>
        /// Lookup for global services, IE services that are not scoped to the underlying project.
        /// </summary>
        /// <typeparam name="T">Type of a service to look for.</typeparam>
        /// <returns>Desired service instance or <code>null</code> if not found.</returns>
        T GetGlobalService<T>() where T : class;
    }
}
