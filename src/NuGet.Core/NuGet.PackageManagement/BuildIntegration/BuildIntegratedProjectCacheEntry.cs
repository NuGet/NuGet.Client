// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Represents the state of a build integrated project.
    /// </summary>
    public class BuildIntegratedProjectCacheEntry
    {
        public BuildIntegratedProjectCacheEntry(
            string projectConfigPath,
            IEnumerable<string> referenceClosure,
            IEnumerable<string> supportsProfiles)
        {
            if (projectConfigPath == null)
            {
                throw new ArgumentNullException(nameof(projectConfigPath));
            }

            if (referenceClosure == null)
            {
                throw new ArgumentNullException(nameof(referenceClosure));
            }

            if (supportsProfiles == null)
            {
                throw new ArgumentNullException(nameof(supportsProfiles));
            }

            ProjectConfigPath = projectConfigPath;
            ReferenceClosure = new HashSet<string>(referenceClosure);
            SupportsProfiles = supportsProfiles;
        }

        /// <summary>
        /// The build integrated project for this entry.
        /// </summary>
        public string ProjectConfigPath { get; }

        /// <summary>
        /// All project.json files and msbuild references in the closure.
        /// </summary>
        public HashSet<string> ReferenceClosure { get; }

        public IEnumerable<string> SupportsProfiles { get; }
    }
}
