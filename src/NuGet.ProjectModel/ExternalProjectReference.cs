// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Represents a reference to a project produced by an external build system, such as msbuild.
    /// </summary>
    public class ExternalProjectReference
    {
        /// <summary>
        /// Represents a reference to a project produced by an external build system, such as msbuild.
        /// </summary>
        /// <param name="uniqueName">unique project name or full path</param>
        /// <param name="packageSpecPath">project.json path</param>
        /// <param name="projectReferences">unique names of the referenced projects</param>
        public ExternalProjectReference(string uniqueName, string packageSpecPath, IEnumerable<string> projectReferences)
        {
            if (uniqueName == null)
            {
                throw new ArgumentNullException(nameof(uniqueName));
            }

            if (projectReferences == null)
            {
                throw new ArgumentNullException(nameof(projectReferences));
            }

            UniqueName = uniqueName;
            PackageSpecPath = packageSpecPath;
            ExternalProjectReferences = projectReferences.ToList();
        }

        /// <summary>
        /// Unique name of the external project
        /// </summary>
        public string UniqueName { get; }

        /// <summary>
        /// The path to the nuget.json file representing the NuGet dependencies of the project
        /// </summary>
        public string PackageSpecPath { get; }

        /// <summary>
        /// A list of other external projects this project references
        /// </summary>
        public IReadOnlyList<string> ExternalProjectReferences { get; }
    }
}
