// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.ProjectModel;

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
        /// <param name="uniqueName">unique project name or full path</param>
        /// <param name="packageSpec">project.json file or null if none exists</param>
        /// <param name="msbuildProjectPath">project file if one exists</param>
        /// <param name="projectReferences">unique names of the referenced projects</param>
        public BuildIntegratedProjectReference(
            string uniqueName,
            PackageSpec packageSpec,
            string msbuildProjectPath,
            IEnumerable<string> projectReferences)
        {
            if (uniqueName == null)
            {
                throw new ArgumentNullException(nameof(uniqueName));
            }

            if (projectReferences == null)
            {
                throw new ArgumentNullException(nameof(projectReferences));
            }

            Name = uniqueName;
            PackageSpec = packageSpec;
            MSBuildProjectPath = msbuildProjectPath;
            ExternalProjectReferences = projectReferences.ToList();
        }

        /// <summary>
        /// The name of the external project
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// project.json file
        /// </summary>
        public PackageSpec PackageSpec { get; }

        /// <summary>
        /// A list of other external projects this project references. Uses <see cref="Name"/>
        /// </summary>
        public IReadOnlyList<string> ExternalProjectReferences { get; }

        /// <summary>
        /// Path to msbuild project file. Ex: xproj, csproj
        /// </summary>
        public string MSBuildProjectPath { get; }
    }
}
