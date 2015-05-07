// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using VSLangProj;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// A nuget aware project system containing a .json file instead of a packages.config file
    /// </summary>
    public class BuildIntegratedProjectSystem : BuildIntegratedNuGetProject
    {
        public BuildIntegratedProjectSystem(string jsonConfigPath, EnvDTEProject envDTEProject, IMSBuildNuGetProjectSystem msbuildProjectSystem, string projectName, string uniqueName)
            : base(jsonConfigPath, msbuildProjectSystem)
        {
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, uniqueName);

            EnvDTEProject = envDTEProject;
        }

        /// <summary>
        /// DTE project
        /// </summary>
        protected EnvDTEProject EnvDTEProject { get; }

        /// <summary>
        /// Returns the closure of all project to project references below this project.
        /// </summary>
        /// <remarks>This uses DTE and should be called from the UI thread.</remarks>
        public override async Task<IReadOnlyList<BuildIntegratedProjectReference>> GetProjectReferenceClosureAsync()
        {
            // DTE calls need to be done from the main thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var results = new List<BuildIntegratedProjectReference>();

            // projects to walk
            var toProcess = new Queue<EnvDTEProject>();

            // start with the current project
            toProcess.Enqueue(EnvDTEProject);

            // continue walking all project references until we run out
            while (toProcess.Count > 0)
            {
                var project = toProcess.Dequeue();

                // find the project.json file if it exists in the project
                // projects with no project.json file should use null for the spec path
                var jsonConfigItem = project.ProjectItems.OfType<ProjectItem>()
                    .FirstOrDefault(pi => StringComparer.Ordinal.Equals(pi.Name, BuildIntegratedProjectUtility.ProjectConfigFileName))?.FileNames[0];

                var childReferences = new List<string>();

                // find all references in the project
                foreach (var childReference in GetProjectReferences(project))
                {
                    if (childReference.SourceProject != null)
                    {
                        string childName = childReference.SourceProject.UniqueName;

                        childReferences.Add(childName);

                        // avoid looping by checking if we already have this project
                        if (!results.Any(projReference => StringComparer.Ordinal.Equals(projReference.Name, childName)))
                        {
                            toProcess.Enqueue(childReference.SourceProject);
                        }
                    }
                }

                results.Add(new BuildIntegratedProjectReference(project.UniqueName, jsonConfigItem, childReferences));
            }

            return results;
        }

        private static IEnumerable<Reference> GetProjectReferences(EnvDTEProject project)
        {
            var langProject = project.Object as VSProject;
            if (langProject != null)
            {
                foreach (var reference in langProject.References.Cast<Reference>())
                {
                    yield return reference;
                }
            }
        }
    }
}
