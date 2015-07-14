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
        public BuildIntegratedProjectCacheEntry(string projectConfigPath, IEnumerable<string> packageSpecClosure)
        {
            if (projectConfigPath == null)
            {
                throw new ArgumentNullException(nameof(projectConfigPath));
            }

            if (packageSpecClosure == null)
            {
                throw new ArgumentNullException(nameof(packageSpecClosure));
            }

            ProjectConfigPath = projectConfigPath;
            PackageSpecClosure = new HashSet<string>(packageSpecClosure);
        }

        /// <summary>
        /// The build integrated project for this entry.
        /// </summary>
        public string ProjectConfigPath { get; }

        /// <summary>
        /// All project.json files in the closure.
        /// </summary>
        public HashSet<string> PackageSpecClosure { get; }
    }
}
