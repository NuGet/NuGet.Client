// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using VSLangProj;
using VSLangProj80;
using EnvDTEProject = EnvDTE.Project;
using Threading = System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// A nuget aware project system containing a .json file instead of a packages.config file
    /// </summary>
    public class BuildIntegratedProjectSystem : BuildIntegratedNuGetProject
    {
        private IScriptExecutor _scriptExecutor;

        public BuildIntegratedProjectSystem(
            string jsonConfigPath,
            EnvDTEProject envDTEProject,
            IMSBuildNuGetProjectSystem msbuildProjectSystem,
            string uniqueName)
            : base(jsonConfigPath, msbuildProjectSystem)
        {
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, uniqueName);

            EnvDTEProject = envDTEProject;
        }

        /// <summary>
        /// DTE project
        /// </summary>
        protected EnvDTEProject EnvDTEProject { get; }

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

        /// <summary>
        /// Returns the closure of all project to project references below this project.
        /// </summary>
        /// <remarks>This uses DTE and should be called from the UI thread.</remarks>
        public override async Task<IReadOnlyList<ExternalProjectReference>> GetProjectReferenceClosureAsync(
            ExternalProjectReferenceContext context)
        {
            var logger = context.Logger;
            var cache = context.Cache;

            // DTE calls need to be done from the main thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var results = new List<ExternalProjectReference>();

            // projects to walk
            var toProcess = new Queue<EnvDTEProject>();

            // start with the current project
            toProcess.Enqueue(EnvDTEProject);

            // keep track of found projects to avoid duplicates
            var uniqueProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootProjectPath = EnvDTEProjectUtility.GetFullProjectPath(EnvDTEProject);

            uniqueProjects.Add(rootProjectPath);

            var itemsFactory = ServiceLocator.GetInstance<IVsEnumHierarchyItemsFactory>();

            // continue walking all project references until we run out
            while (toProcess.Count > 0)
            {
                var project = toProcess.Dequeue();

                // Find the path of the current project
                var projectFileFullPath = EnvDTEProjectUtility.GetFullProjectPath(project);

                IReadOnlyList<ExternalProjectReference> cacheReferences;
                if (cache.TryGetValue(projectFileFullPath, out cacheReferences))
                {
                    // The cached value contains the entire closure, add it to the results and skip
                    // all child references.
                    results.AddRange(cacheReferences);
                }
                else
                {
                    // Find projectName.project.json first
                    var projectName = Path.GetFileNameWithoutExtension(projectFileFullPath);

                    var fileWithProjectName =
                        BuildIntegratedProjectUtility.GetProjectConfigWithProjectName(projectName);

                    string jsonConfigItem = null;

                    // Loop through all root project items. Sub folders will not be part of this.
                    jsonConfigItem = GetJsonConfigFromProject(project, fileWithProjectName);

                    // Verify ReferenceOutputAssembly
                    var excludedProjects = GetExcludedReferences(project, itemsFactory);

                    var childReferences = new List<string>();
                    var hasMissingReferences = false;

                    // find all references in the project
                    foreach (var childReference in GetProjectReferences(project))
                    {
                        try
                        {
                            var reference3 = childReference as Reference3;

                            if (reference3 != null && !reference3.Resolved)
                            {
                                // Skip missing references and show a warning
                                hasMissingReferences = true;
                                continue;
                            }

                            // Skip missing references
                            if (childReference.SourceProject != null)
                            {
                                var childName = EnvDTEProjectUtility.GetFullProjectPath(childReference.SourceProject);

                                // Skip projects which have ReferenceOutputAssembly=false
                                if (!excludedProjects.Contains(childName, StringComparer.OrdinalIgnoreCase))
                                {
                                    childReferences.Add(childName);

                                    // avoid looping by checking if we already have this project
                                    if (!uniqueProjects.Contains(childName))
                                    {
                                        toProcess.Enqueue(childReference.SourceProject);
                                        uniqueProjects.Add(childName);
                                    }
                                }
                            }
                            else
                            {
                                // SDK references do not have a SourceProject or child references, 
                                // but they can contain project.json files, and should be part of the closure
                                // SDKs are not projects, only the project.json name is checked here

                                var possibleSdkPath = childReference.Path;

                                if (!string.IsNullOrEmpty(possibleSdkPath) && Directory.Exists(possibleSdkPath))
                                {
                                    var possibleProjectJson = Path.Combine(
                                                                possibleSdkPath,
                                                                BuildIntegratedProjectUtility.ProjectConfigFileName);

                                    if (File.Exists(possibleProjectJson))
                                    {
                                        childReferences.Add(possibleProjectJson);

                                        var projectSpec = context.GetOrCreateSpec(
                                            childReference.Name,
                                            possibleProjectJson);

                                        // add the sdk to the results here
                                        results.Add(new ExternalProjectReference(
                                            possibleProjectJson,
                                            projectSpec,
                                            msbuildProjectPath: null,
                                            projectReferences: Enumerable.Empty<string>()));
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Exceptions are expected in some scenarios for native projects,
                            // ignore them and show a warning
                            hasMissingReferences = true;

                            Debug.Fail("Unable to find project closure: " + ex.ToString());
                        }
                    }

                    if (hasMissingReferences)
                    {
                        // Log a warning message once per project
                        // This warning contains only the names of the root project and the project with the
                        // broken reference. Attempting to display more details on the actual reference 
                        // that has the problem may lead to another exception being thrown.
                        var warning = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Warning_ErrorDuringProjectClosureWalk,
                            projectName,
                            rootProjectPath);

                        logger.LogWarning(warning);
                    }

                    // Add the current project (include the root) to the results
                    PackageSpec packageSpec = null;
                    if (File.Exists(jsonConfigItem))
                    {
                        // Not all projects have a project.json, only read it if it exists
                        packageSpec = context.GetOrCreateSpec(projectName, jsonConfigItem);
                    }

                    // For the xproj -> xproj -> csproj scenario find all xproj-> xproj references.
                    if (projectFileFullPath.EndsWith(XProjUtility.XProjExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        // All xproj paths, these are already checked for project.json
                        var xprojFiles = XProjUtility.GetProjectReferences(projectFileFullPath);

                        if (xprojFiles.Count > 0)
                        {
                            var pathToProject = GetPathToDTEProjectLookup(project);

                            foreach (var xProjPath in xprojFiles)
                            {
                                // Only add projects that we can find in the solution, otherwise they will
                                // end up causing failures. If this is an actual failure the resolver will
                                // fail when resolving the dependency from project.json
                                Project xProjDTE;
                                if (pathToProject.TryGetValue(xProjPath, out xProjDTE))
                                {
                                    var xProjFullPath = EnvDTEProjectUtility.GetFullProjectPath(xProjDTE);
                                    childReferences.Add(xProjFullPath);

                                    // Continue walking this project if it has not been walked already
                                    if (!uniqueProjects.Contains(xProjFullPath))
                                    {
                                        toProcess.Enqueue(xProjDTE);
                                    }
                                }
                            }
                        }
                    }

                    results.Add(new ExternalProjectReference(
                        projectFileFullPath,
                        packageSpec,
                        projectFileFullPath,
                        childReferences));
                }
            }

            // Cache the results
            if (!cache.ContainsKey(rootProjectPath))
            {
                // Create a new copy of the list so that callers cannot modify it
                cache.Add(rootProjectPath, new List<ExternalProjectReference>(results));
            }

            return results;
        }

        /// <summary>
        /// Find project.json or projectName.project.json
        /// </summary>
        private static string GetJsonConfigFromProject(EnvDTEProject project, string fileWithProjectName)
        {
            string jsonConfigItem = null;

            foreach (var filePath in project.ProjectItems.OfType<ProjectItem>()
                                    .Select(p => p.FileNames[1]))
            {
                if (!BuildIntegratedProjectUtility.IsProjectConfig(filePath))
                {
                    continue;
                }

                // The filename is also checked in BuildIntegratedProjectUtility.IsProjectConfig, if it
                // is invalid, it will return false above.
                var fileName = Path.GetFileName(filePath);

                // Check for projName.project.json
                if (string.Equals(
                        fileWithProjectName,
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    jsonConfigItem = filePath;
                    break;
                }

                // Fallback to project.json if projName.project.json does not exist
                if (string.Equals(
                        BuildIntegratedProjectUtility.ProjectConfigFileName,
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    jsonConfigItem = filePath;
                }
            }

            return jsonConfigItem;
        }

        private static Dictionary<string, EnvDTEProject> GetPathToDTEProjectLookup(EnvDTEProject project)
        {
            var pathToProject
                   = new Dictionary<string, EnvDTEProject>(StringComparer.OrdinalIgnoreCase);

            foreach (var solutionProjectObj in project.DTE.Solution.Projects)
            {
                var solutionProject = solutionProjectObj as Project;

                if (solutionProject != null)
                {
                    var solutionProjectPath = EnvDTEProjectUtility.GetFullProjectPath(solutionProject);

                    if (!string.IsNullOrEmpty(solutionProjectPath)
                        && !pathToProject.ContainsKey(solutionProjectPath))
                    {
                        pathToProject.Add(solutionProjectPath, solutionProject);
                    }
                }
            }

            return pathToProject;
        }

        public override async Threading.Task<bool> ExecuteInitScriptAsync(
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
                            initPS1RelativePath =
                                ProjectManagement.PathUtility
                                .ReplaceAltDirSeparatorWithDirSeparator(initPS1RelativePath);

                            return await ScriptExecutor.ExecuteAsync(
                                identity,
                                packageInstallPath,
                                initPS1RelativePath,
                                EnvDTEProject,
                                this,
                                projectContext,
                                throwOnFailure);
                        }
                    }
                }
            }

            return false;
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

        /// <summary>
        /// Get the unique names of all references which have ReferenceOutputAssembly set to false.
        /// </summary>
        private static List<string> GetExcludedReferences(
            EnvDTEProject project,
            IVsEnumHierarchyItemsFactory itemsFactory)
        {
            var excludedReferences = new List<string>();

            var hierarchy = VsHierarchyUtility.ToVsHierarchy(project);

            // Get all items in the hierarchy, this includes project references, files, and everything else.
            IEnumHierarchyItems items;
            if (ErrorHandler.Succeeded(itemsFactory.EnumHierarchyItems(
                hierarchy,
                (uint)__VSEHI.VSEHI_Leaf,
                (uint)VSConstants.VSITEMID.Root,
                out items)))
            {
                var buildPropertyStorage = (IVsBuildPropertyStorage)hierarchy;

                // Loop through all items
                uint fetched;
                VSITEMSELECTION[] item = new VSITEMSELECTION[1];
                while (ErrorHandler.Succeeded(items.Next(1, item, out fetched)) && fetched == 1)
                {
                    // Check if the item has ReferenceOutputAssembly. This will
                    // return null for the vast majority of items.
                    string value;
                    if (ErrorHandler.Succeeded(buildPropertyStorage.GetItemAttribute(
                            item[0].itemid,
                            "ReferenceOutputAssembly",
                            out value))
                        && value != null)
                    {
                        // We only need to go farther if the flag exists and is not true
                        if (!string.Equals(value, Boolean.TrueString, StringComparison.OrdinalIgnoreCase))
                        {
                            // Get the DTE Project reference for the item id. This checks for nulls incase this is 
                            // somehow not a project reference that had the ReferenceOutputAssembly flag.
                            object childObject;
                            if (ErrorHandler.Succeeded(hierarchy.GetProperty(
                                item[0].itemid,
                                (int)__VSHPROPID.VSHPROPID_ExtObject,
                                out childObject)))
                            {
                                // 1. Verify that this is a project reference
                                // 2. Check that it is valid and resolved
                                // 3. Follow the reference to the DTE project and get the unique name
                                var reference = childObject as Reference3;

                                if (reference != null && reference.Resolved && reference.SourceProject != null)
                                {
                                    var childPath = EnvDTEProjectUtility
                                        .GetFullProjectPath(reference.SourceProject);

                                    excludedReferences.Add(childPath);
                                }
                            }
                        }
                    }
                }
            }

            return excludedReferences;
        }
    }
}
