// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio.Common;
using Task = System.Threading.Tasks.Task;
using TaskExpandedNodes = System.Threading.Tasks.Task<System.Collections.Generic.IDictionary<string, System.Collections.Generic.ISet<NuGet.VisualStudio.VsHierarchyItem>>>;

namespace NuGet.VisualStudio
{
    public static class VsHierarchyUtility
    {
        private const string VsWindowKindSolutionExplorer = "3AE79031-E1BC-11D0-8F78-00A0C9110057";

        private static readonly string[] UnsupportedProjectCapabilities = new string[]
        {
            "SharedAssetsProject", // This is true for shared projects in universal apps
        };

        public static string GetProjectPath(IVsHierarchy project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            project.GetCanonicalName(VSConstants.VSITEMID_ROOT, out string projectPath);
            return projectPath;
        }

        public static bool IsSupported(IVsHierarchy hierarchy, string projectTypeGuid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IsProjectCapabilityCompliant(hierarchy))
            {
                return true;
            }

            return !string.IsNullOrEmpty(projectTypeGuid) && SupportedProjectTypes.IsSupported(projectTypeGuid) && !HasUnsupportedProjectCapability(hierarchy);
        }

        public static bool IsProjectCapabilityCompliant(IVsHierarchy hierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // NOTE: (AssemblyReferences + DeclaredSourceItems + UserSourceItems) exists solely for compatibility reasons
            // with existing custom CPS-based projects that existed before "PackageReferences" capability was introduced.
            return hierarchy.IsCapabilityMatch("(AssemblyReferences + DeclaredSourceItems + UserSourceItems) | PackageReferences");
        }

        public static bool HasUnsupportedProjectCapability(IVsHierarchy hierarchy)
        {
            return UnsupportedProjectCapabilities.Any(c => hierarchy.IsCapabilityMatch(c));
        }

        public static string[] GetProjectTypeGuids(IVsHierarchy hierarchy, string defaultType = "")
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var aggregatableProject = hierarchy as IVsAggregatableProject;
            if (aggregatableProject != null)
            {
                string projectTypeGuids;
                var hr = aggregatableProject.GetAggregateProjectTypeGuids(out projectTypeGuids);
                ErrorHandler.ThrowOnFailure(hr);

                return projectTypeGuids.Split(';');
            }

            if (!string.IsNullOrEmpty(defaultType))
            {
                return new[] { defaultType };
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Check for CPS capability in IVsHierarchy. All CPS projects will have CPS capability except VisualC projects.
        /// So checking for VisualC explicitly with a OR flag.
        /// </summary>
        public static bool IsCPSCapabilityComplaint(IVsHierarchy hierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return hierarchy.IsCapabilityMatch("CPS | VisualC");
        }

        /// <summary>
        /// Gets the EnvDTE.Project instance from IVsHierarchy
        /// </summary>
        /// <param name="pHierarchy">pHierarchy is the IVsHierarchy instance from which the project instance is obtained</param>
        public static EnvDTE.Project GetProjectFromHierarchy(IVsHierarchy pHierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Set it to null to avoid unassigned local variable warning
            EnvDTE.Project project = null;
            object projectObject;

            if (pHierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out projectObject) >= 0)
            {
                project = (EnvDTE.Project)projectObject;
            }

            return project;
        }

        public static async TaskExpandedNodes GetAllExpandedNodesAsync()
        {
            // this operation needs to execute on UI thread
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            var projects = dte.Solution.Projects;

            var results = new Dictionary<string, ISet<VsHierarchyItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in projects.Cast<EnvDTE.Project>())
            {
                var expandedNodes =
                    GetExpandedProjectHierarchyItems(project);
                Debug.Assert(!results.ContainsKey(EnvDTEProjectInfoUtility.GetUniqueName(project)));
                results[EnvDTEProjectInfoUtility.GetUniqueName(project)] =
                    new HashSet<VsHierarchyItem>(expandedNodes);
            }
            return results;
        }

        public static async Task CollapseAllNodesAsync(IDictionary<string, ISet<VsHierarchyItem>> ignoreNodes)
        {
            Verify.ArgumentIsNotNull(ignoreNodes, nameof(ignoreNodes));

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            var projects = dte.Solution.Projects;

            foreach (var project in projects.Cast<EnvDTE.Project>())
            {
                ISet<VsHierarchyItem> expandedNodes;
                if (ignoreNodes.TryGetValue(EnvDTEProjectInfoUtility.GetUniqueName(project), out expandedNodes)
                    &&
                    expandedNodes != null)
                {
                    CollapseProjectHierarchyItems(project, expandedNodes);
                }
            }
        }

        private static ICollection<VsHierarchyItem> GetExpandedProjectHierarchyItems(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectHierarchyItem = VsHierarchyItem.FromDteProject(project);
            var solutionExplorerWindow = GetSolutionExplorerHierarchyWindow();

            if (solutionExplorerWindow == null)
            {
                // If the solution explorer is collapsed since opening VS, this value is null. In such a case, simply exit early.
                return new VsHierarchyItem[0];
            }

            var expandedItems = new List<VsHierarchyItem>();

            // processCallback return values: 
            //     0   continue, 
            //     1   don't recurse into, 
            //    -1   stop
            projectHierarchyItem.WalkDepthFirst(
                fVisible: true,
                processCallback:
                    (VsHierarchyItem vsItem, object callerObject, out object newCallerObject) =>
                    {
                        newCallerObject = null;
                        if (IsVsHierarchyItemExpanded(vsItem, solutionExplorerWindow))
                        {
                            expandedItems.Add(vsItem);
                        }
                        return 0;
                    },
                callerObject: null);

            return expandedItems;
        }

        private static void CollapseProjectHierarchyItems(EnvDTE.Project project, ISet<VsHierarchyItem> ignoredHierarcyItems)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectHierarchyItem = VsHierarchyItem.FromDteProject(project);
            var solutionExplorerWindow = GetSolutionExplorerHierarchyWindow();

            if (solutionExplorerWindow == null)
            {
                // If the solution explorer is collapsed since opening VS, this value is null. In such a case, simply exit early.
                return;
            }

            // processCallback return values:
            //     0   continue, 
            //     1   don't recurse into, 
            //    -1   stop
            projectHierarchyItem.WalkDepthFirst(
                fVisible: true,
                processCallback:
                    (VsHierarchyItem currentHierarchyItem, object callerObject, out object newCallerObject) =>
                    {
                        newCallerObject = null;
                        if (!ignoredHierarcyItems.Contains(currentHierarchyItem))
                        {
                            CollapseVsHierarchyItem(currentHierarchyItem, solutionExplorerWindow);
                        }
                        return 0;
                    },
                callerObject: null);
        }

        private static void CollapseVsHierarchyItem(VsHierarchyItem vsHierarchyItem, IVsUIHierarchyWindow vsHierarchyWindow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (vsHierarchyItem == null
                || vsHierarchyWindow == null)
            {
                return;
            }

            vsHierarchyWindow.ExpandItem(AsVsUIHierarchy(vsHierarchyItem), vsHierarchyItem.VsItemID, EXPANDFLAGS.EXPF_CollapseFolder);
        }

        private static bool IsVsHierarchyItemExpanded(VsHierarchyItem hierarchyItem, IVsUIHierarchyWindow uiWindow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!hierarchyItem.IsExpandable())
            {
                return false;
            }

            const uint expandedStateMask = (uint)__VSHIERARCHYITEMSTATE.HIS_Expanded;
            uint itemState;

            uiWindow.GetItemState(AsVsUIHierarchy(hierarchyItem), hierarchyItem.VsItemID, expandedStateMask, out itemState);
            return ((__VSHIERARCHYITEMSTATE)itemState == __VSHIERARCHYITEMSTATE.HIS_Expanded);
        }

        private static IVsUIHierarchy AsVsUIHierarchy(VsHierarchyItem hierarchyItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return hierarchyItem.VsHierarchy as IVsUIHierarchy;
        }

        private static IVsUIHierarchyWindow GetSolutionExplorerHierarchyWindow()
        {
            return VsShellUtilities.GetUIHierarchyWindow(
                ServiceLocator.GetInstance<IServiceProvider>(),
                new Guid(VsWindowKindSolutionExplorer));
        }
    }
}
