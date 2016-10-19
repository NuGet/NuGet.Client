// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// A nuget aware project system containing a .json file instead of a packages.config file
    /// </summary>
    public class BuildIntegratedProjectSystem : BuildIntegratedNuGetProject
    {
        private readonly EnvDTEProject _envDTEProject;
        private IScriptExecutor _scriptExecutor;

        public BuildIntegratedProjectSystem(
            string jsonConfigPath,
            string msbuildProjectFilePath,
            EnvDTEProject envDTEProject,
            IMSBuildNuGetProjectSystem msbuildProjectSystem,
            string uniqueName)
            : base(jsonConfigPath, msbuildProjectFilePath, msbuildProjectSystem)
        {
            _envDTEProject = envDTEProject;

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
            if (ScriptExecutor != null)
            {
                var packageReader = new PackageFolderReader(packageInstallPath);

                var toolItemGroups = packageReader.GetToolItems();

                if (toolItemGroups != null)
                {
                    // Init.ps1 must be found at the root folder, target frameworks are not recognized here,
                    // since this is run for the solution.
                    var toolItemGroup = toolItemGroups
                                        .Where(group => group.TargetFramework.IsAny)
                                        .FirstOrDefault();

                    if (toolItemGroup != null)
                    {
                        var initPS1RelativePath = toolItemGroup.Items
                            .Where(p => p.StartsWith(
                                PowerShellScripts.InitPS1RelativePath,
                                StringComparison.OrdinalIgnoreCase))
                            .FirstOrDefault();

                        if (!string.IsNullOrEmpty(initPS1RelativePath))
                        {
                            initPS1RelativePath = PathUtility
                                .ReplaceAltDirSeparatorWithDirSeparator(initPS1RelativePath);

                            return await ScriptExecutor.ExecuteAsync(
                                identity,
                                packageInstallPath,
                                initPS1RelativePath,
                                _envDTEProject,
                                projectContext,
                                throwOnFailure);
                        }
                    }
                }
            }

            return false;
        }

        public override Task<IReadOnlyList<IDependencyGraphProject>> GetDirectProjectReferencesAsync(DependencyGraphCacheContext context)
        {
            var solutionManager = (VSSolutionManager)ServiceLocator.GetInstance<ISolutionManager>();
            var list = new List<IDependencyGraphProject>();
            if (solutionManager != null && EnvDTEProjectUtility.SupportsReferences(_envDTEProject))
            {
                foreach (var referencedProject in EnvDTEProjectUtility.GetReferencedProjects(_envDTEProject))
                {
                    var nugetProject = EnvDTEProjectUtility.GetNuGetProject(referencedProject, solutionManager);
                    var dependencyGraphProject = nugetProject as IDependencyGraphProject;
                    if (dependencyGraphProject != null)
                    {
                        list.Add(dependencyGraphProject);
                    }
                }
            }

            return Task.FromResult<IReadOnlyList<IDependencyGraphProject>>(list.AsReadOnly());
        }
    }
}
