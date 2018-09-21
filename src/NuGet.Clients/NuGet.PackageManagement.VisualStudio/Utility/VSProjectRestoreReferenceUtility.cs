// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
using NuGet.PackageManagement.UI;
using NuGet.ProjectModel;
using VSLangProj;
using VSLangProj80;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class VSProjectRestoreReferenceUtility
    {
        /// <summary>
        /// Get only the direct dependencies from a project
        /// </summary>
        public static async Task<IReadOnlyList<ProjectRestoreReference>> GetDirectProjectReferences(
            EnvDTEProject project,
            IEnumerable<string> resolvedProjects,
            ILogger log)
        {
            return await NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                // DTE calls need to be done from the main thread
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var results = new List<ProjectRestoreReference>();

                var itemsFactory = ServiceLocator.GetInstance<IVsEnumHierarchyItemsFactory>();

                // Verify ReferenceOutputAssembly
                var excludedProjects = GetExcludedReferences(project, itemsFactory);
                var hasMissingReferences = false;

                // find all references in the project
                foreach (var childReference in GetProjectReferences(project))
                {
                    try
                    {
                        var reference3 = childReference as Reference3;

                        // check if deferred projects resolved this reference, which means this is still not loaded so simply continue
                        // We'll get this reference from deferred projects later
                        if (reference3 != null &&
                        resolvedProjects.Contains(reference3.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Set missing reference if
                        // 1. reference is null OR
                        // 2. reference is not resolved which means project is not loaded or assembly not found.
                        else if (reference3 == null || !reference3.Resolved)
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

                            var childProjectPath = EnvDTEProjectUtility.GetFullProjectPath(childReference.SourceProject);

                            // Skip projects which have ReferenceOutputAssembly=false
                            if (!string.IsNullOrEmpty(childProjectPath)
                                && !excludedProjects.Contains(childProjectPath, StringComparer.OrdinalIgnoreCase))
                            {
                                var restoreReference = new ProjectRestoreReference()
                                {
                                    ProjectPath = childProjectPath,
                                    ProjectUniqueName = childProjectPath
                                };

                                results.Add(restoreReference);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Exceptions are expected in some scenarios for native projects,
                        // ignore them and show a warning
                        hasMissingReferences = true;

                        log.LogDebug(ex.ToString());

                        Debug.Fail("Unable to find project dependencies: " + ex.ToString());
                    }
                }

                if (hasMissingReferences)
                {
                    // Log a generic message once per project if any items could not be resolved.
                    // In most cases this can be ignored, but in the rare case where the unresolved
                    // item is actually a project the restore result will be incomplete.
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UnresolvedItemDuringProjectClosureWalk,
                        EnvDTEProjectUtility.GetUniqueName(project));

                    log.LogVerbose(message);
                }

                return results;
            });
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