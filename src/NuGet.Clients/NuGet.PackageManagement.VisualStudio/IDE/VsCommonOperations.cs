// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ICommonOperations))]
    internal sealed class VsCommonOperations : ICommonOperations
    {
        private readonly Lazy<EnvDTE.DTE> _dte;
        private IDictionary<string, ISet<VsHierarchyItem>> _expandedNodes;

        [ImportingConstructor]
        public VsCommonOperations(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _dte = new Lazy<EnvDTE.DTE>(
                () => serviceProvider.GetDTE());
        }

        public Task OpenFile(string fullPath)
        {
            if (fullPath == null)
            {
                throw new ArgumentNullException(nameof(fullPath));
            }

            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (_dte.Value.ItemOperations != null
                        && File.Exists(fullPath))
                    {
                        var window = _dte.Value.ItemOperations.OpenFile(fullPath);
                        return Task.FromResult(0);
                    }

                    return Task.CompletedTask;
                });
        }

        public Task SaveSolutionExplorerNodeStates(ISolutionManager solutionManager)
        {
            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _expandedNodes = await VsHierarchyUtility.GetAllExpandedNodesAsync();

                return Task.CompletedTask;
            });
        }

        public Task CollapseAllNodes(ISolutionManager solutionManager)
        {
            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await VsHierarchyUtility.CollapseAllNodesAsync(_expandedNodes);

                return Task.CompletedTask;
            });
        }
    }
}
