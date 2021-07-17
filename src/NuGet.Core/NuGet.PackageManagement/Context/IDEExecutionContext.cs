// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement
{
    public class IDEExecutionContext : ExecutionContext
    {
        public ICommonOperations CommonOperations { get; }

        public IRenderReadMeMarkdownToolWindow Render { get; }

        public IDEExecutionContext(ICommonOperations commonOperations, IRenderReadMeMarkdownToolWindow renderReadMeMarkdownToolWindow)
        {
            if (commonOperations == null)
            {
                throw new ArgumentNullException(nameof(commonOperations));
            }

            if (renderReadMeMarkdownToolWindow == null)
            {
                throw new ArgumentNullException(nameof(renderReadMeMarkdownToolWindow));
            }
            CommonOperations = commonOperations;
            Render = renderReadMeMarkdownToolWindow;
        }

        public override async Task OpenFile(string fullPath)
        {
            await CommonOperations.OpenFile(fullPath);
        }

        public override async Task RenderMarkDownFile(string fullPath)
        {
            await Render.DisplayReadMeMarkdownToolWindowAsync(fullPath);
        }

        public async Task SaveExpandedNodeStates(ISolutionManager solutionManager)
        {
            await CommonOperations.SaveSolutionExplorerNodeStates(solutionManager);
        }

        public async Task CollapseAllNodes(ISolutionManager solutionManager)
        {
            await CommonOperations.CollapseAllNodes(solutionManager);
        }

        public PackageIdentity IDEDirectInstall
        {
            get { return DirectInstall; }
            set { DirectInstall = value; }
        }
    }
}
