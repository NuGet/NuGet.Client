// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
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
        public BuildIntegratedProjectSystem(string jsonConfigPath, EnvDTEProject envDTEProject, IMSBuildNuGetProjectSystem msbuildProjectSystem, string uniqueName)
            : base(jsonConfigPath, msbuildProjectSystem)
        {
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

            // keep track of found projects to avoid duplicates
            var uniqueProjects = new HashSet<string>();
            var rootProjectName = await EnvDTEProjectUtility.GetCustomUniqueNameAsync(EnvDTEProject);
            uniqueProjects.Add(rootProjectName);

            // continue walking all project references until we run out
            while (toProcess.Count > 0)
            {
                var project = toProcess.Dequeue();

                // find the project.json file if it exists in the project
                // projects with no project.json file should use null for the spec path
                var jsonConfigItem = project.ProjectItems.OfType<ProjectItem>()
                    .FirstOrDefault(pi => StringComparer.Ordinal.Equals(pi.Name, BuildIntegratedProjectUtility.ProjectConfigFileName))?.FileNames[0];

                var projectUniqueName = await EnvDTEProjectUtility.GetCustomUniqueNameAsync(project);

                var childReferences = new List<string>();

                // find all references in the project
                foreach (var childReference in GetProjectReferences(project))
                {
                    if (childReference.SourceProject != null)
                    {
                        var childName = await EnvDTEProjectUtility.GetCustomUniqueNameAsync(childReference.SourceProject);

                        childReferences.Add(childName);

                        // avoid looping by checking if we already have this project
                        if (!uniqueProjects.Contains(childName))
                        {
                            toProcess.Enqueue(childReference.SourceProject);
                            uniqueProjects.Add(childName);
                        }
                    }
                    else
                    {
                        // SDK references do not have a SourceProject or child references, 
                        // but they can contain project.json files, and should be part of the closure
                        var possibleSdkPath = childReference.Path;
                        if (!String.IsNullOrEmpty(possibleSdkPath) && Directory.Exists(possibleSdkPath))
                        {
                            var possibleProjectJson = Path.Combine(childReference.Path, BuildIntegratedProjectUtility.ProjectConfigFileName);
                            if (File.Exists(possibleProjectJson))
                            {
                                childReferences.Add(possibleProjectJson);

                                // add the sdk to the results here
                                results.Add(new BuildIntegratedProjectReference(possibleProjectJson, possibleProjectJson, Enumerable.Empty<string>()));
                            }
                        }
                    }
                }

                if (!string.Equals(rootProjectName, projectUniqueName, StringComparison.OrdinalIgnoreCase))
                {
                    // Don't add the project we're trying to resolve the closure for to the result
                    results.Add(new BuildIntegratedProjectReference(projectUniqueName, jsonConfigItem, childReferences));
                }
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
