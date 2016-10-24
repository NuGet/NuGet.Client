// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
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
            ILogger log)
        {
            return await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                // DTE calls need to be done from the main thread
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
                    // Log a warning message once per project
                    // This warning contains only the names of the root project and the project with the
                    // broken reference. Attempting to display more details on the actual reference
                    // that has the problem may lead to another exception being thrown.
                    var warning = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Warning_ErrorDuringProjectClosureWalk,
                        EnvDTEProjectUtility.GetUniqueName(project));

                    log.LogWarning(warning);
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