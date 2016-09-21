// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// The context object for caching information about a project's own project references. These caches updated and
    /// used during the restore no-op detection and restore operation itself.
    /// </summary>
    public class ExternalProjectReferenceContext
    {
        public ExternalProjectReferenceContext(ILogger logger)
            : this(projectCache: null, logger: logger)
        {
        }
        
        public ExternalProjectReferenceContext(
            Dictionary<string, DependencyGraphProjectCacheEntry> projectCache,
            ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            Logger = logger;

            DirectReferenceCache = new Dictionary<string, IReadOnlyList<ExternalProjectReference>>(
                StringComparer.OrdinalIgnoreCase);

            ClosureCache = new Dictionary<string, IReadOnlyList<ExternalProjectReference>>(
                StringComparer.OrdinalIgnoreCase);

            ProjectCache = projectCache ?? new Dictionary<string, DependencyGraphProjectCacheEntry>(
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// A cache of a project's direct references. The key is the full path to the project.
        /// </summary>
        public IDictionary<string, IReadOnlyList<ExternalProjectReference>> DirectReferenceCache { get; }

        /// <summary>
        /// A cache of the full closure of project references. The key is the full path to the project.
        /// </summary>
        public IDictionary<string, IReadOnlyList<ExternalProjectReference>> ClosureCache { get; }

        /// <summary>
        /// A cache of the files in a project that can have references and a last modified time. In practice, this is
        /// a list of all project.json and MSBuild project files in a closure. The key is the full path to the MSBuild
        /// project file.
        /// </summary>
        public Dictionary<string, DependencyGraphProjectCacheEntry> ProjectCache { get; set; }

        /// <summary>
        /// Logger
        /// </summary>
        public ILogger Logger { get; }
    }
}
