// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.VisualStudio;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// A nuget aware project system containing a .json file instead of a packages.config file
    /// </summary>
    public class ProjectJsonBuildIntegratedProjectSystem : ProjectJsonBuildIntegratedNuGetProject
    {
        private readonly EnvDTEProject _envDTEProject;
        private IScriptExecutor _scriptExecutor;

        public ProjectJsonBuildIntegratedProjectSystem(
            string jsonConfigPath,
            string msbuildProjectFilePath,
            EnvDTEProject envDTEProject,
            string uniqueName)
            : base(jsonConfigPath, msbuildProjectFilePath)
        {
            _envDTEProject = envDTEProject;

            // set project id
            var projectId = VsHierarchyUtility.GetProjectId(envDTEProject);
            InternalMetadata.Add(NuGetProjectMetadataKeys.ProjectId, projectId);

            // Override the JSON TFM value from the DTE here.
            var platfromVersion = EnvDTEProjectInfoUtility.GetTargetPlatformVersion(envDTEProject);
            var platfromMinVersion = EnvDTEProjectInfoUtility.GetTargetPlatformMinVersion(envDTEProject);
            if (platfromMinVersion == null)
            {
                platfromMinVersion = VsHierarchyUtility.GetMSBuildProperty(VsHierarchyUtility.ToVsHierarchy(envDTEProject), "TargetPlatformMinVersion");
            }

            // Found the TPFmV in csproj, replace the json target framework value with this one.
            if (platfromMinVersion != null)
            {
                NuGetFramework newTargetFramework = null;
                if (InternalMetadata.ContainsKey(NuGetProjectMetadataKeys.TargetFramework))
                {
                    var jsonTargetFramework = InternalMetadata[NuGetProjectMetadataKeys.TargetFramework] as NuGetFramework;
                    newTargetFramework = new NuGetFramework(jsonTargetFramework.Framework, new Version(platfromMinVersion));
                    InternalMetadata[NuGetProjectMetadataKeys.TargetFramework] = newTargetFramework;
                }
            }

            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, uniqueName);
        }

        private IScriptExecutor ScriptExecutor
        {
            get
            {
                if (_scriptExecutor == null)
                {
                    _scriptExecutor = ServiceLocator.GetInstanceSafe<IScriptExecutor>();
                }

                return _scriptExecutor;
            }
        }

        public override async Task<bool> ExecuteInitScriptAsync(
            PackageIdentity identity,
            string packageInstallPath,
            INuGetProjectContext projectContext,
            bool throwOnFailure)
        {
            return
                await
                    ScriptExecutorUtil.ExecuteScriptAsync(identity, packageInstallPath, projectContext, ScriptExecutor,
                        _envDTEProject, throwOnFailure);
        }

        public override Task<IReadOnlyList<ProjectRestoreReference>> GetDirectProjectReferencesAsync(DependencyGraphCacheContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var resolvedProjects = context.DeferredPackageSpecs.Select(project => project.Name);
            return VSProjectRestoreReferenceUtility.GetDirectProjectReferences(_envDTEProject, resolvedProjects, context.Logger);
        }
    }
}
