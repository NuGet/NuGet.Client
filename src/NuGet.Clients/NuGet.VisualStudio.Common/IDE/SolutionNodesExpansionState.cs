// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Task = System.Threading.Tasks.Task;

namespace NuGet.VisualStudio
{
    /// <summary> Class representing expansion state of solution nodes. </summary>
    public sealed class SolutionNodesExpansionState : IDisposable
    {
        private static readonly Guid VsWindowKindSolutionExplorer = new Guid("3AE79031-E1BC-11D0-8F78-00A0C9110057");

        private enum TraversalAction
        {
            Continue = 0,
            DoNotRecurse = 1,
            Stop = -1,
        }

        private delegate Task<(TraversalAction action, object newCallerObject)> ProcessItemDelegate(VsHierarchyItem item, object callerObject);

        private readonly IDictionary<string, ISet<VsHierarchyItem>> _expandedNodes;

        private SolutionNodesExpansionState(IDictionary<string, ISet<VsHierarchyItem>> expandedNodes)
        {
            _expandedNodes = expandedNodes;
        }

        /// <summary> Save expansion state of solution nodes. </summary>
        /// <returns> An opaque object storing state of nodes, can be disposed to restore state. </returns>
        public static async Task<IDisposable> SaveAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            var projects = dte.Solution.Projects;

            var results = new Dictionary<string, ISet<VsHierarchyItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in projects.Cast<EnvDTE.Project>())
            {
                var expandedNodes = await GetExpandedProjectHierarchyItemsAsync(project);
                Debug.Assert(!results.ContainsKey(EnvDTEProjectInfoUtility.GetUniqueName(project)));
                results[EnvDTEProjectInfoUtility.GetUniqueName(project)] = new HashSet<VsHierarchyItem>(expandedNodes);
            }

            return new SolutionNodesExpansionState(results);
        }

        /// <summary> Restore state of solution nodes. </summary>
        public void Dispose()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
                var projects = dte.Solution.Projects;

                foreach (var project in projects.Cast<EnvDTE.Project>())
                {
                    if (_expandedNodes.TryGetValue(EnvDTEProjectInfoUtility.GetUniqueName(project), out var expandedNodes)
                     && expandedNodes != null)
                    {
                        await CollapseProjectHierarchyItemsAsync(project, expandedNodes);
                    }
                }
            });
        }

        private static async Task<ICollection<VsHierarchyItem>> GetExpandedProjectHierarchyItemsAsync(EnvDTE.Project project)
        {
            var projectHierarchyItem = new VsHierarchyItem((await VsHierarchy.FromDteProjectAsync(project)).Ptr, VSConstants.VSITEMID_ROOT);
            var solutionExplorerWindow = GetSolutionExplorerHierarchyWindow();

            if (solutionExplorerWindow == null)
            {
                // If the solution explorer is collapsed since opening VS, this value is null. In such a case, simply exit early.
                return new VsHierarchyItem[0];
            }

            var expandedItems = new List<VsHierarchyItem>();
            await projectHierarchyItem.WalkDepthFirstAsync(
                visible: true,
                processCallbackAsync:
                async (VsHierarchyItem vsItem, object callerObject) =>
                {
                    if (await vsItem.IsVsHierarchyItemExpandedAsync(solutionExplorerWindow))
                    {
                        expandedItems.Add(vsItem);
                    }

                    return (TraversalAction.Continue, null);
                },
                callerObject: null);

            return expandedItems;
        }

        private static async Task CollapseProjectHierarchyItemsAsync(EnvDTE.Project project, ISet<VsHierarchyItem> ignoredHierarcyItems)
        {
            var projectHierarchyItem = new VsHierarchyItem((await VsHierarchy.FromDteProjectAsync(project)).Ptr, VSConstants.VSITEMID_ROOT);
            var solutionExplorerWindow = GetSolutionExplorerHierarchyWindow();

            if (solutionExplorerWindow == null)
            {
                // If the solution explorer is collapsed since opening VS, this value is null. In such a case, simply exit early.
                return;
            }

            await projectHierarchyItem.WalkDepthFirstAsync(
                visible: true,
                processCallbackAsync:
                async (VsHierarchyItem currentHierarchyItem, object callerObject) =>
                {
                    if (!ignoredHierarcyItems.Contains(currentHierarchyItem))
                    {
                        await currentHierarchyItem.CollapseAsync(solutionExplorerWindow);
                    }

                    return (TraversalAction.Continue, null);
                },
                callerObject: null);
        }

        private static IVsUIHierarchyWindow GetSolutionExplorerHierarchyWindow()
        {
            return VsShellUtilities.GetUIHierarchyWindow(
                ServiceLocator.GetInstance<IServiceProvider>(),
                VsWindowKindSolutionExplorer);
        }

        private class VsHierarchyItem : IEquatable<VsHierarchyItem>
        {
            private readonly uint _vsitemid;
            private readonly IVsHierarchy _hierarchy;

            internal VsHierarchyItem(IVsHierarchy hierarchy, uint id)
            {
                _vsitemid = id;
                _hierarchy = hierarchy;
            }

            internal async Task<TraversalAction> WalkDepthFirstAsync(bool visible, ProcessItemDelegate processCallbackAsync, object callerObject)
            {
                // TODO Need to see what to do if this is a sub project
                //
                // Get the node type
                // Guid nodeGuid;
                // if (hier.GetGuidProperty(_vsitemid, (int)__VSHPROPID.VSHPROPID_TypeGuid, out nodeGuid) != 0)

                if (processCallbackAsync == null)
                {
                    return TraversalAction.Continue;
                }

                (var processReturn, var newCallerObject) = await processCallbackAsync(this, callerObject);
                if (processReturn != TraversalAction.Continue)
                {
                    // Callback says to skip (1) or stop (-1)
                    return processReturn;
                }

                // The process callback can change the caller object. If not we just use the one originally
                // passed in.
                if (newCallerObject == null)
                {
                    newCallerObject = callerObject;
                }

                // Walk children if there are any
                if (await IsExpandableAsync())
                {
                    var child = await GetFirstChildAsync(visible);
                    while (child != null)
                    {
                        object isNonMemberItemValue = await child.GetPropertyAsync(__VSHPROPID.VSHPROPID_IsNonMemberItem);

                        // Some project systems (e.g. F#) don't support querying for the VSHPROPID_IsNonMemberItem property.
                        // In that case, we treat this child as belonging to the project
                        bool isMemberOfProject = isNonMemberItemValue == null || (bool)isNonMemberItemValue == false;
                        if (isMemberOfProject)
                        {
                            var returnVal = await child.WalkDepthFirstAsync(visible, processCallbackAsync, newCallerObject);
                            if (returnVal == TraversalAction.Stop)
                            {
                                return returnVal;
                            }
                        }

                        child = await child.GetNextSiblingAsync(visible);
                    }
                }

                return TraversalAction.Continue;
            }

            internal async Task<bool> IsVsHierarchyItemExpandedAsync(IVsUIHierarchyWindow uiWindow)
            {
                if (!await IsExpandableAsync())
                {
                    return false;
                }

                const uint expandedStateMask = (uint)__VSHIERARCHYITEMSTATE.HIS_Expanded;
                uint itemState;

                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                uiWindow.GetItemState(await AsVsUIHierarchyAsync(), _vsitemid, expandedStateMask, out itemState);
                return ((__VSHIERARCHYITEMSTATE)itemState == __VSHIERARCHYITEMSTATE.HIS_Expanded);
            }

            internal async Task CollapseAsync(IVsUIHierarchyWindow vsHierarchyWindow)
            {
                if (vsHierarchyWindow == null)
                {
                    return;
                }

                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                vsHierarchyWindow.ExpandItem(await AsVsUIHierarchyAsync(), _vsitemid, EXPANDFLAGS.EXPF_CollapseFolder);
            }

            public bool Equals(VsHierarchyItem other)
            {
                return _vsitemid == other._vsitemid;
            }

            public override bool Equals(object obj)
            {
                var other = obj as VsHierarchyItem;
                return other != null && Equals(other);
            }

            public override int GetHashCode()
            {
                return _vsitemid.GetHashCode();
            }

            private async Task<bool> IsExpandableAsync()
            {
                var o = await GetPropertyAsync(__VSHPROPID.VSHPROPID_Expandable);
                if (o is bool)
                {
                    return (bool)o;
                }

                return (o is int) && (int)o != 0;
            }

            private async Task<object> GetPropertyAsync(__VSHPROPID propid)
            {
                (var succeeded, var value) = await TryGetPropertyAsync((int)propid);

                if (succeeded)
                {
                    return value;
                }

                return null;
            }

            private async Task<(bool succeeded, object value)> TryGetPropertyAsync(int propid)
            {
                object value = null;
                try
                {
                    if (_hierarchy != null)
                    {
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        _hierarchy.GetProperty(_vsitemid, propid, out value);
                    }

                    return (true, value);
                }
                catch
                {
                    return (false, value);
                }
            }

            private async Task<VsHierarchyItem> GetNextSiblingAsync(bool visible)
            {
                uint childId = await GetNextSiblingIdAsync(visible);
                if (childId != VSConstants.VSITEMID_NIL)
                {
                    return new VsHierarchyItem(_hierarchy, childId);
                }
                return null;
            }

            private async Task<uint> GetNextSiblingIdAsync(bool visible)
            {
                object o = await GetPropertyAsync(visible ? __VSHPROPID.VSHPROPID_NextVisibleSibling : __VSHPROPID.VSHPROPID_NextSibling);

                if (o is int)
                {
                    return unchecked((uint)((int)o));
                }

                if (o is uint)
                {
                    return (uint)o;
                }

                return VSConstants.VSITEMID_NIL;
            }

            private async Task<VsHierarchyItem> GetFirstChildAsync(bool visible)
            {
                uint childId = await GetFirstChildIdAsync(visible);
                if (childId != VSConstants.VSITEMID_NIL)
                {
                    return new VsHierarchyItem(_hierarchy, childId);
                }

                return null;
            }

            private async Task<uint> GetFirstChildIdAsync(bool visible)
            {
                object o = await GetPropertyAsync(visible ? __VSHPROPID.VSHPROPID_FirstVisibleChild : __VSHPROPID.VSHPROPID_FirstChild);

                if (o is int)
                {
                    return unchecked((uint)((int)o));
                }

                if (o is uint)
                {
                    return (uint)o;
                }

                return VSConstants.VSITEMID_NIL;
            }

            private async Task<IVsUIHierarchy> AsVsUIHierarchyAsync()
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return _hierarchy as IVsUIHierarchy;
            }
        }
    }
}
