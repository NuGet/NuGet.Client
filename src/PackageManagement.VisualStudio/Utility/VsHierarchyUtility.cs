// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using TaskExpandedNodes = System.Threading.Tasks.Task<System.Collections.Generic.IDictionary<string, System.Collections.Generic.ISet<NuGet.PackageManagement.VisualStudio.VsHierarchyItem>>>;


namespace NuGet.PackageManagement.VisualStudio
{
    public static class VsHierarchyUtility
    {
        private const string VsWindowKindSolutionExplorer = "3AE79031-E1BC-11D0-8F78-00A0C9110057";

        public static IVsHierarchy ToVsHierarchy(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsHierarchy hierarchy;

            // Get the vs solution
            IVsSolution solution = ServiceLocator.GetInstance<IVsSolution>();
            int hr = solution.GetProjectOfUniqueName(EnvDTEProjectUtility.GetUniqueName(project), out hierarchy);

            if (hr != NuGetVSConstants.S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return hierarchy;
        }

        public static string[] GetProjectTypeGuids(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the vs hierarchy as an IVsAggregatableProject to get the project type guids
            var hierarchy = ToVsHierarchy(project);
            var aggregatableProject = hierarchy as IVsAggregatableProject;
            if (aggregatableProject != null)
            {
                string projectTypeGuids;
                int hr = aggregatableProject.GetAggregateProjectTypeGuids(out projectTypeGuids);

                if (hr != NuGetVSConstants.S_OK)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                return projectTypeGuids.Split(';');
            }
            if (!String.IsNullOrEmpty(project.Kind))
            {
                return new[] { project.Kind };
            }
            return new string[0];
        }

        /// <summary>
        /// Gets the EnvDTE.Project instance from IVsHierarchy
        /// </summary>
        /// <param name="pHierarchy">pHierarchy is the IVsHierarchy instance from which the project instance is obtained</param>
        public static Project GetProjectFromHierarchy(IVsHierarchy pHierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Set it to null to avoid unassigned local variable warning
            Project project = null;
            object projectObject;

            if (pHierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out projectObject) >= 0)
            {
                project = (Project)projectObject;
            }

            return project;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "solutionManager")]
        public static async TaskExpandedNodes GetAllExpandedNodesAsync(ISolutionManager solutionManager)
        {
            // this operation needs to execute on UI thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<DTE>();
            var projects = dte.Solution.Projects;

            var results = new Dictionary<string, ISet<VsHierarchyItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (Project project in projects)
            {
                ICollection<VsHierarchyItem> expandedNodes =
                    GetExpandedProjectHierarchyItems(project);
                Debug.Assert(!results.ContainsKey(EnvDTEProjectUtility.GetUniqueName(project)));
                results[EnvDTEProjectUtility.GetUniqueName(project)] =
                    new HashSet<VsHierarchyItem>(expandedNodes);
            }
            return results;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "solutionManager")]
        public static async Task CollapseAllNodesAsync(ISolutionManager solutionManager, IDictionary<string, ISet<VsHierarchyItem>> ignoreNodes)
        {
            // this operation needs to execute on UI thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<DTE>();
            var projects = dte.Solution.Projects;

            foreach (Project project in projects)
            {
                ISet<VsHierarchyItem> expandedNodes;
                if (ignoreNodes.TryGetValue(EnvDTEProjectUtility.GetUniqueName(project), out expandedNodes)
                    &&
                    expandedNodes != null)
                {
                    CollapseProjectHierarchyItems(project, expandedNodes);
                }
            }
        }

        private static ICollection<VsHierarchyItem> GetExpandedProjectHierarchyItems(Project project)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            VsHierarchyItem projectHierarchyItem = GetHierarchyItemForProject(project);
            IVsUIHierarchyWindow solutionExplorerWindow = GetSolutionExplorerHierarchyWindow();

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

        private static void CollapseProjectHierarchyItems(Project project, ISet<VsHierarchyItem> ignoredHierarcyItems)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            VsHierarchyItem projectHierarchyItem = GetHierarchyItemForProject(project);
            IVsUIHierarchyWindow solutionExplorerWindow = GetSolutionExplorerHierarchyWindow();

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

        private static VsHierarchyItem GetHierarchyItemForProject(Project project)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            IVsHierarchy hierarchy;

            // Get the solution
            IVsSolution solution = ServiceLocator.GetGlobalService<SVsSolution, IVsSolution>();
            int hr = solution.GetProjectOfUniqueName(EnvDTEProjectUtility.GetUniqueName(project), out hierarchy);

            if (hr != VSConstants.S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            return new VsHierarchyItem(hierarchy);
        }

        private static void CollapseVsHierarchyItem(VsHierarchyItem vsHierarchyItem, IVsUIHierarchyWindow vsHierarchyWindow)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            if (vsHierarchyItem == null
                || vsHierarchyWindow == null)
            {
                return;
            }

            vsHierarchyWindow.ExpandItem(vsHierarchyItem.UIHierarchy(), vsHierarchyItem.VsItemID, EXPANDFLAGS.EXPF_CollapseFolder);
        }

        private static bool IsVsHierarchyItemExpanded(VsHierarchyItem hierarchyItem, IVsUIHierarchyWindow uiWindow)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            if (!hierarchyItem.IsExpandable())
            {
                return false;
            }

            const uint expandedStateMask = (uint)__VSHIERARCHYITEMSTATE.HIS_Expanded;
            uint itemState;

            uiWindow.GetItemState(hierarchyItem.UIHierarchy(), hierarchyItem.VsItemID, expandedStateMask, out itemState);
            return ((__VSHIERARCHYITEMSTATE)itemState == __VSHIERARCHYITEMSTATE.HIS_Expanded);
        }

        private static IVsUIHierarchyWindow GetSolutionExplorerHierarchyWindow()
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            return VsShellUtilities.GetUIHierarchyWindow(
                ServiceLocator.GetInstance<IServiceProvider>(),
                new Guid(VsWindowKindSolutionExplorer));
        }
    }
}
