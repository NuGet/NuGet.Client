// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.VisualStudio
{
    /// <summary> Class representing expansion state of solution nodes. </summary>
    public sealed class SolutionNodesExpansionState : IDisposable
    {
        private static readonly Guid VsWindowKindSolutionExplorer = new Guid("3AE79031-E1BC-11D0-8F78-00A0C9110057");

        private IDictionary<string, ISet<VsHierarchy>> _expandedNodes;

        private SolutionNodesExpansionState(IDictionary<string, ISet<VsHierarchy>> expandedNodes)
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

            var results = new Dictionary<string, ISet<VsHierarchy>>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in projects.Cast<EnvDTE.Project>())
            {
                var expandedNodes =
                    GetExpandedProjectHierarchyItems(project);
                Debug.Assert(!results.ContainsKey(EnvDTEProjectInfoUtility.GetUniqueName(project)));
                results[EnvDTEProjectInfoUtility.GetUniqueName(project)] =
                    new HashSet<VsHierarchy>(expandedNodes);
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
                    ISet<VsHierarchy> expandedNodes;
                    if (_expandedNodes.TryGetValue(EnvDTEProjectInfoUtility.GetUniqueName(project), out expandedNodes)
                        &&
                        expandedNodes != null)
                    {
                        CollapseProjectHierarchyItems(project, expandedNodes);
                    }
                }
            });
        }


        private static ICollection<VsHierarchy> GetExpandedProjectHierarchyItems(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectHierarchyItem = VisualStudio.VsHierarchy.FromDteProject(project);
            var solutionExplorerWindow = GetSolutionExplorerHierarchyWindow();

            if (solutionExplorerWindow == null)
            {
                // If the solution explorer is collapsed since opening VS, this value is null. In such a case, simply exit early.
                return new VsHierarchy[0];
            }

            var expandedItems = new List<VsHierarchy>();

            // processCallback return values: 
            //     0   continue, 
            //     1   don't recurse into, 
            //    -1   stop
            projectHierarchyItem.WalkDepthFirst(
                fVisible: true,
                processCallback:
                    (VsHierarchy vsItem, object callerObject, out object newCallerObject) =>
                    {
                        newCallerObject = null;
                        if (vsItem.IsVsHierarchyItemExpanded(solutionExplorerWindow))
                        {
                            expandedItems.Add(vsItem);
                        }
                        return 0;
                    },
                callerObject: null);

            return expandedItems;
        }

        private static void CollapseProjectHierarchyItems(EnvDTE.Project project, ISet<VsHierarchy> ignoredHierarcyItems)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectHierarchyItem = VisualStudio.VsHierarchy.FromDteProject(project);
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
                    (VsHierarchy currentHierarchyItem, object callerObject, out object newCallerObject) =>
                    {
                        newCallerObject = null;
                        if (!ignoredHierarcyItems.Contains(currentHierarchyItem))
                        {
                            currentHierarchyItem.Collapse(solutionExplorerWindow);
                        }
                        return 0;
                    },
                callerObject: null);
        }

        private static IVsUIHierarchyWindow GetSolutionExplorerHierarchyWindow()
        {
            return VsShellUtilities.GetUIHierarchyWindow(
                ServiceLocator.GetInstance<IServiceProvider>(),
                VsWindowKindSolutionExplorer);
        }
    }
}
