// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// A simple class to hold project references. It is copied from ExternalProjectReference to reduce
    /// dependencies.
    /// </summary>
    public class BuildIntegratedProjectReference
    {
        /// <summary>
        /// Represents a reference to a project produced by an external build system, such as msbuild.
        /// </summary>
        /// <param name="name">unique project name or full path</param>
        /// <param name="packageSpecPath">project.json path</param>
        /// <param name="externalProjectReferences">unique names of the referenced projects</param>
        public BuildIntegratedProjectReference(string name, string packageSpecPath, IEnumerable<string> externalProjectReferences)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (externalProjectReferences == null)
            {
                throw new ArgumentNullException(nameof(externalProjectReferences));
            }

            Name = name;
            PackageSpecPath = packageSpecPath;
            ExternalProjectReferences = externalProjectReferences.ToList();
        }

        /// <summary>
        /// The name of the external project
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The path to the project.json file representing the NuGet dependencies of the project
        /// </summary>
        public string PackageSpecPath { get; }

        /// <summary>
        /// A list of other external projects this project references
        /// </summary>
        public IReadOnlyList<string> ExternalProjectReferences { get; }
    }
}
