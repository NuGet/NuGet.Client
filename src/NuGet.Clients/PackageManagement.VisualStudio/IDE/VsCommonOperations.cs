// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ICommonOperations))]
    public class VsCommonOperations : ICommonOperations
    {
        private readonly DTE _dte;
        private IDictionary<string, ISet<VsHierarchyItem>> _expandedNodes;

        public VsCommonOperations()
            : this(ServiceLocator.GetInstance<DTE>())
        {
        }

        public VsCommonOperations(DTE dte)
        {
            _dte = dte;
        }

        public Task OpenFile(string fullPath)
        {
            if (fullPath == null)
            {
                throw new ArgumentNullException(nameof(fullPath));
            }

            return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (_dte.ItemOperations != null
                        && File.Exists(fullPath))
                    {
                        Window window = _dte.ItemOperations.OpenFile(fullPath);
                        return Task.FromResult(0);
                    }

                    return Task.FromResult(0);
                });
        }

        public Task SaveSolutionExplorerNodeStates(ISolutionManager solutionManager)
        {
            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _expandedNodes = await VsHierarchyUtility.GetAllExpandedNodesAsync(solutionManager);

                return Task.FromResult(0);
            });
        }

        public Task CollapseAllNodes(ISolutionManager solutionManager)
        {
            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await VsHierarchyUtility.CollapseAllNodesAsync(solutionManager, _expandedNodes);

                return Task.FromResult(0);
            });
        }
    }
}
