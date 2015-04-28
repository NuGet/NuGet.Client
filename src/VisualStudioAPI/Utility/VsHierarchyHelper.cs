using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using Task = System.Threading.Tasks.Task;
using TaskExpandedNodes = System.Threading.Tasks.Task<System.Collections.Generic.IDictionary<string, System.Collections.Generic.ISet<NuGet.VisualStudio.VsHierarchyItem>>>;

namespace NuGet.VisualStudio
{
    internal static class VsHierarchyHelper
    {
        private const string VsWindowKindSolutionExplorer = "3AE79031-E1BC-11D0-8F78-00A0C9110057";
        private const string WixProjectTypeGuid = "930C7802-8A8C-48F9-8165-68863BCCD9DD";

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
                Debug.Assert(!results.ContainsKey(GetUniqueName(project)));
                results[GetUniqueName(project)] =
                    new HashSet<VsHierarchyItem>(expandedNodes);
            }
            return results;
        }

        public static async Task CollapseAllNodesAsync(ISolutionManager solutionManager, IDictionary<string, ISet<VsHierarchyItem>> ignoreNodes)
        {
            // this operation needs to execute on UI thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<DTE>();
            var projects = dte.Solution.Projects;

            foreach (Project project in projects)
            {
                ISet<VsHierarchyItem> expandedNodes;
                if (ignoreNodes.TryGetValue(GetUniqueName(project), out expandedNodes) &&
                    expandedNodes != null)
                {
                    CollapseProjectHierarchyItems(project, expandedNodes);
                }
            }
        }

        private static ICollection<VsHierarchyItem> GetExpandedProjectHierarchyItems(EnvDTE.Project project)
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

        private static VsHierarchyItem GetHierarchyItemForProject(EnvDTE.Project project)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            IVsHierarchy hierarchy;

            // Get the solution
            IVsSolution solution = ServiceLocator.GetGlobalService<SVsSolution, IVsSolution>();
            int hr = solution.GetProjectOfUniqueName(GetUniqueName(project), out hierarchy);

            if (hr != VSConstants.S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            return new VsHierarchyItem(hierarchy);
        }

        private static void CollapseVsHierarchyItem(VsHierarchyItem vsHierarchyItem, IVsUIHierarchyWindow vsHierarchyWindow)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            if (vsHierarchyItem == null || vsHierarchyWindow == null)
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

        private static string GetUniqueName(Project project)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            if (IsWixProject(project))
            {
                // Wix project doesn't offer UniqueName property
                return project.FullName;
            }

            try
            {
                return project.UniqueName;
            }
            catch (COMException)
            {
                return project.FullName;
            }
        }

        private static bool IsWixProject(Project project)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            return project.Kind != null && project.Kind.Equals(WixProjectTypeGuid, StringComparison.OrdinalIgnoreCase);
        }
    }
}