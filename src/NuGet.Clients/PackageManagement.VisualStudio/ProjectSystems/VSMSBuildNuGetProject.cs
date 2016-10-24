﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;


namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// This is an implementation of <see cref="MSBuildNuGetProject"/> that has knowledge about interacting with DTE.
    /// Since the base class <see cref="MSBuildNuGetProject"/> is in the NuGet.Core solution, it does not have
    /// references to DTE.
    /// </summary>
    public class VSMSBuildNuGetProject : MSBuildNuGetProject
    {
        private readonly EnvDTEProject _project;

        public VSMSBuildNuGetProject(
            EnvDTEProject project,
            IMSBuildNuGetProjectSystem msbuildNuGetProjectSystem,
            string folderNuGetProjectPath,
            string packagesConfigFolderPath) : base(
                msbuildNuGetProjectSystem,
                folderNuGetProjectPath,
                packagesConfigFolderPath)
        {
            _project = project;

            // set project id
            var projectId = VsHierarchyUtility.GetProjectId(project);
            InternalMetadata.Add(NuGetProjectMetadataKeys.ProjectId, projectId);
        }

        public override async Task<IReadOnlyList<IDependencyGraphProject>> GetDirectProjectReferencesAsync(DependencyGraphCacheContext context)
        {
            IReadOnlyList<IDependencyGraphProject> references = null;
            if (context == null || !context.DirectReferenceCache.TryGetValue(MSBuildProjectPath, out references))
            {
                var solutionManager = (VSSolutionManager)ServiceLocator.GetInstance<ISolutionManager>();
                var list = new List<IDependencyGraphProject>();
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (solutionManager != null && EnvDTEProjectUtility.SupportsReferences(_project))
                {
                    foreach (var referencedProject in EnvDTEProjectUtility.GetReferencedProjects(_project))
                    {
                        var nugetProject = EnvDTEProjectUtility.GetNuGetProject(referencedProject, solutionManager);
                        var dependencyGraphProject = nugetProject as IDependencyGraphProject;
                        if (dependencyGraphProject != null)
                        {
                            list.Add(dependencyGraphProject);
                        }
                    }
                }
                references = list.AsReadOnly();
                context?.DirectReferenceCache.Add(MSBuildProjectPath, references);
            }

            return await Task.FromResult<IReadOnlyList<IDependencyGraphProject>>(references);
        }
    }
}