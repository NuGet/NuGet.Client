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
using NuGet.Common;
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
            string msbuildProjectFilePath,
            EnvDTEProject envDTEProject,
            IMSBuildNuGetProjectSystem msbuildProjectSystem,
            string uniqueName)
            : base(jsonConfigPath, msbuildProjectFilePath, msbuildProjectSystem)
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

            var results = new HashSet<ExternalProjectReference>();

            // projects to walk - DFS
            var toProcess = new Stack<DTEReference>();

            // keep track of found projects to avoid duplicates
            var rootProjectPath = EnvDTEProjectUtility.GetFullProjectPath(EnvDTEProject);

            // start with the current project
            toProcess.Push(new DTEReference(EnvDTEProject, rootProjectPath));

            var itemsFactory = ServiceLocator.GetInstance<IVsEnumHierarchyItemsFactory>();
            var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // continue walking all project references until we run out
            while (toProcess.Count > 0)
            {
                var dteReference = toProcess.Pop();

                // Find the path of the current project
                var projectFileFullPath = dteReference.Path;
                var project = dteReference.Project;

                if (string.IsNullOrEmpty(projectFileFullPath) || !uniqueNames.Add(projectFileFullPath))
                {
                    // This has already been processed or does not exist
                    continue;
                }

                IReadOnlyList<ExternalProjectReference> cacheReferences;
                if (cache.TryGetValue(projectFileFullPath, out cacheReferences))
                {
                    // The cached value contains the entire closure, add it to the results and skip
                    // all child references.
                    results.UnionWith(cacheReferences);
                }
                else
                {
                    // Get direct references
                    var projectResult = GetDirectProjectReferences(
                        dteReference.Project,
                        projectFileFullPath,
                        context,
                        itemsFactory,
                        rootProjectPath);

                    // Add results to the closure
                    results.UnionWith(projectResult.Processed);

                    // Continue processing
                    foreach (var item in projectResult.ToProcess)
                    {
                        toProcess.Push(item);
                    }
                }
            }

            // Cache the results for this project and every child project which has not been cached
            foreach (var project in results)
            {
                if (!context.Cache.ContainsKey(project.UniqueName))
                {
                    var closure = BuildIntegratedRestoreUtility.GetExternalClosure(project.UniqueName, results);

                    context.Cache.Add(project.UniqueName, closure.ToList());
                }
            }

            return cache[rootProjectPath];
        }

        /// <summary>
        /// Get only the direct dependencies from a project
        /// </summary>
        private DirectReferences GetDirectProjectReferences(
            EnvDTEProject project,
            string projectFileFullPath,
            ExternalProjectReferenceContext context,
            IVsEnumHierarchyItemsFactory itemsFactory,
            string rootProjectPath)
        {
            var logger = context.Logger;
            var cache = context.Cache;

            var result = new DirectReferences();

            // Find a project.json in the project
            // This checks for files on disk to match how BuildIntegratedProjectSystem checks at creation time.
            // NuGet.exe also uses disk paths and not the project file.
            var projectName = Path.GetFileNameWithoutExtension(projectFileFullPath);

            var projectDirectory = Path.GetDirectoryName(projectFileFullPath);

            // Check for projectName.project.json and project.json
            string jsonConfigItem =
                ProjectJsonPathUtilities.GetProjectConfigPath(
                    directoryPath: projectDirectory,
                    projectName: projectName);

            var hasProjectJson = true;

            // Verify the file exists, otherwise clear it
            if (!File.Exists(jsonConfigItem))
            {
                jsonConfigItem = null;
                hasProjectJson = false;
            }

            // Verify ReferenceOutputAssembly
            var excludedProjects = GetExcludedReferences(project, itemsFactory);

            var childReferences = new HashSet<string>(StringComparer.Ordinal);
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
                        if (EnvDTEProjectUtility.HasUnsupportedProjectCapability(childReference.SourceProject))
                        {
                            // Skip this shared project
                            continue;
                        }

                        var childName = EnvDTEProjectUtility.GetFullProjectPath(childReference.SourceProject);

                        // Skip projects which have ReferenceOutputAssembly=false
                        if (!string.IsNullOrEmpty(childName)
                            && !excludedProjects.Contains(childName, StringComparer.OrdinalIgnoreCase))
                        {
                            childReferences.Add(childName);

                            result.ToProcess.Add(new DTEReference(childReference.SourceProject, childName));
                        }
                    }
                    else if (hasProjectJson)
                    {
                        // SDK references do not have a SourceProject or child references, 
                        // but they can contain project.json files, and should be part of the closure
                        // SDKs are not projects, only the project.json name is checked here
                        var possibleSdkPath = childReference.Path;

                        if (!string.IsNullOrEmpty(possibleSdkPath)
                            && !possibleSdkPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                            && Directory.Exists(possibleSdkPath))
                        {
                            var possibleProjectJson = Path.Combine(
                                                        possibleSdkPath,
                                                        ProjectJsonPathUtilities.ProjectConfigFileName);

                            if (File.Exists(possibleProjectJson))
                            {
                                childReferences.Add(possibleProjectJson);

                                // add the sdk to the results here
                                result.Processed.Add(new ExternalProjectReference(
                                    possibleProjectJson,
                                    childReference.Name,
                                    possibleProjectJson,
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

                    logger.LogDebug(ex.ToString());

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

                            if (!string.IsNullOrEmpty(xProjFullPath))
                            {
                                childReferences.Add(xProjFullPath);

                                // Continue walking this project if it has not been walked already
                                result.ToProcess.Add(new DTEReference(xProjDTE, xProjFullPath));
                            }
                        }
                    }
                }
            }

            // Only set a package spec project name if a package spec exists
            var packageSpecProjectName = jsonConfigItem == null ? null : projectName;

            // Add the parent project to the results
            result.Processed.Add(new ExternalProjectReference(
                projectFileFullPath,
                packageSpecProjectName,
                jsonConfigItem,
                projectFileFullPath,
                childReferences));

            return result;
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

        /// <summary>
        /// Top level references
        /// </summary>
        private class DirectReferences
        {
            public HashSet<DTEReference> ToProcess { get; } = new HashSet<DTEReference>();

            public HashSet<ExternalProjectReference> Processed { get; } = new HashSet<ExternalProjectReference>();
        }

        /// <summary>
        /// Holds the full path to a project and the DTE object for the project.
        /// </summary>
        private class DTEReference : IEquatable<DTEReference>, IComparable<DTEReference>
        {
            public DTEReference(EnvDTEProject project, string path)
            {
                Project = project;
                Path = path;
            }

            public EnvDTEProject Project { get; }

            public string Path { get; }

            public bool Equals(DTEReference other)
            {
                return StringComparer.Ordinal.Equals(Path, other.Path);
            }

            public int CompareTo(DTEReference other)
            {
                return StringComparer.Ordinal.Compare(Path, other.Path);
            }

            public override int GetHashCode()
            {
                return StringComparer.Ordinal.GetHashCode(Path);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as DTEReference);
            }
        }
    }
}
