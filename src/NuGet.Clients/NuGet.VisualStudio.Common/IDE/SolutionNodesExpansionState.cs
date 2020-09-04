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

        private delegate TraversalAction ProcessItemDelegate(VsHierarchyItem item, object callerObject, out object newCallerObject);

        private readonly IDictionary<string, ISet<VsHierarchyItem>> _expandedNodes;

        private SolutionNodesExpansionState(IDictionary<string, ISet<VsHierarchyItem>> expandedNodes)
        {
            _expandedNodes = expandedNodes;
        }

        /// <summary> Save expansion state of solution nodes. </summary>
        /// <returns> An opaque object storing state of nodes, can be disposed to restore state. </returns>
        public static async Task<IDisposable> SaveAsync()
        {
            // this operation needs to execute on UI thread
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            var projects = dte.Solution.Projects;

            var results = new Dictionary<string, ISet<VsHierarchyItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in projects.Cast<EnvDTE.Project>())
            {
                var expandedNodes = GetExpandedProjectHierarchyItems(project);
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
                // this operation needs to execute on UI thread
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
                var projects = dte.Solution.Projects;

                foreach (var project in projects.Cast<EnvDTE.Project>())
                {
                    if (_expandedNodes.TryGetValue(EnvDTEProjectInfoUtility.GetUniqueName(project), out var expandedNodes)
                     && expandedNodes != null)
                    {
                        CollapseProjectHierarchyItems(project, expandedNodes);
                    }
                }
            });
        }

        private static ICollection<VsHierarchyItem> GetExpandedProjectHierarchyItems(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectHierarchyItem = new VsHierarchyItem(VsHierarchy.FromDteProject(project).Ptr, VSConstants.VSITEMID_ROOT);
            var solutionExplorerWindow = GetSolutionExplorerHierarchyWindow();

            if (solutionExplorerWindow == null)
            {
                // If the solution explorer is collapsed since opening VS, this value is null. In such a case, simply exit early.
                return new VsHierarchyItem[0];
            }

            var expandedItems = new List<VsHierarchyItem>();
            projectHierarchyItem.WalkDepthFirst(
                visible: true,
                processCallback:
                (VsHierarchyItem vsItem, object callerObject, out object newCallerObject) =>
                {
                    newCallerObject = null;
                    if (vsItem.IsVsHierarchyItemExpanded(solutionExplorerWindow))
                    {
                        expandedItems.Add(vsItem);
                    }

                    return TraversalAction.Continue;
                },
                callerObject: null);

            return expandedItems;
        }

        private static void CollapseProjectHierarchyItems(EnvDTE.Project project, ISet<VsHierarchyItem> ignoredHierarcyItems)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectHierarchyItem = new VsHierarchyItem(VsHierarchy.FromDteProject(project).Ptr, VSConstants.VSITEMID_ROOT);
            var solutionExplorerWindow = GetSolutionExplorerHierarchyWindow();

            if (solutionExplorerWindow == null)
            {
                // If the solution explorer is collapsed since opening VS, this value is null. In such a case, simply exit early.
                return;
            }

            projectHierarchyItem.WalkDepthFirst(
                visible: true,
                processCallback:
                (VsHierarchyItem currentHierarchyItem, object callerObject, out object newCallerObject) =>
                {
                    newCallerObject = null;
                    if (!ignoredHierarcyItems.Contains(currentHierarchyItem))
                    {
                        currentHierarchyItem.Collapse(solutionExplorerWindow);
                    }

                    return TraversalAction.Continue;
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

            internal TraversalAction WalkDepthFirst(bool visible, ProcessItemDelegate processCallback, object callerObject)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // TODO Need to see what to do if this is a sub project
                //
                // Get the node type
                // Guid nodeGuid;
                // if (hier.GetGuidProperty(_vsitemid, (int)__VSHPROPID.VSHPROPID_TypeGuid, out nodeGuid) != 0)

                if (processCallback == null)
                {
                    return TraversalAction.Continue;
                }

                object newCallerObject;
                var processReturn = processCallback(this, callerObject, out newCallerObject);
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
                if (IsExpandable())
                {
                    var child = GetFirstChild(visible);
                    while (child != null)
                    {
                        object isNonMemberItemValue = child.GetProperty(__VSHPROPID.VSHPROPID_IsNonMemberItem);
                        // Some project systems (e.g. F#) don't support querying for the VSHPROPID_IsNonMemberItem property.
                        // In that case, we treat this child as belonging to the project
                        bool isMemberOfProject = isNonMemberItemValue == null || (bool)isNonMemberItemValue == false;
                        if (isMemberOfProject)
                        {
                            var returnVal = child.WalkDepthFirst(visible, processCallback, newCallerObject);
                            if (returnVal == TraversalAction.Stop)
                            {
                                return returnVal;
                            }
                        }

                        child = child.GetNextSibling(visible);
                    }
                }

                return 0;
            }

            internal bool IsVsHierarchyItemExpanded(IVsUIHierarchyWindow uiWindow)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (!IsExpandable())
                {
                    return false;
                }

                const uint expandedStateMask = (uint)__VSHIERARCHYITEMSTATE.HIS_Expanded;
                uint itemState;

                uiWindow.GetItemState(AsVsUIHierarchy(), _vsitemid, expandedStateMask, out itemState);
                return ((__VSHIERARCHYITEMSTATE)itemState == __VSHIERARCHYITEMSTATE.HIS_Expanded);
            }

            internal void Collapse(IVsUIHierarchyWindow vsHierarchyWindow)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (vsHierarchyWindow == null)
                {
                    return;
                }

                vsHierarchyWindow.ExpandItem(AsVsUIHierarchy(), _vsitemid, EXPANDFLAGS.EXPF_CollapseFolder);
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

            private bool IsExpandable()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var o = GetProperty(__VSHPROPID.VSHPROPID_Expandable);
                if (o is bool)
                {
                    return (bool)o;
                }
                return (o is int) && (int)o != 0;
            }

            private object GetProperty(__VSHPROPID propid)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (TryGetProperty((int)propid, out var value))
                {
                    return value;
                }

                return null;
            }

            private bool TryGetProperty(int propid, out object value)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                value = null;
                try
                {
                    if (_hierarchy != null)
                    {
                        _hierarchy.GetProperty(_vsitemid, propid, out value);
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private VsHierarchyItem GetNextSibling(bool visible)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                uint childId = GetNextSiblingId(visible);
                if (childId != VSConstants.VSITEMID_NIL)
                {
                    return new VsHierarchyItem(_hierarchy, childId);
                }
                return null;
            }

            private uint GetNextSiblingId(bool visible)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                object o = GetProperty(visible ? __VSHPROPID.VSHPROPID_NextVisibleSibling : __VSHPROPID.VSHPROPID_NextSibling);

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

            private VsHierarchyItem GetFirstChild(bool visible)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                uint childId = GetFirstChildId(visible);
                if (childId != VSConstants.VSITEMID_NIL)
                {
                    return new VsHierarchyItem(_hierarchy, childId);
                }

                return null;
            }

            private uint GetFirstChildId(bool visible)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                object o = GetProperty(visible ? __VSHPROPID.VSHPROPID_FirstVisibleChild : __VSHPROPID.VSHPROPID_FirstChild);

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

            private IVsUIHierarchy AsVsUIHierarchy()
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                return _hierarchy as IVsUIHierarchy;
            }
        }
    }
}
