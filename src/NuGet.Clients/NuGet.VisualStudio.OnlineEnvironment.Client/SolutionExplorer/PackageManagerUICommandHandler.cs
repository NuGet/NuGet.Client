// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace NuGet.VisualStudio.OnlineEnvironment.Client
{
    internal class PackageManagerUICommandHandler
    {
        private const bool IsPackageManagerUISupportAvailable = false;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly IAsyncServiceProvider _asyncServiceProvider;

        public PackageManagerUICommandHandler(JoinableTaskFactory joinableTaskFactory, IAsyncServiceProvider asyncServiceProvider)
        {
            _joinableTaskFactory = joinableTaskFactory ?? throw new ArgumentNullException(nameof(joinableTaskFactory));
            _asyncServiceProvider = asyncServiceProvider ?? throw new ArgumentNullException(nameof(asyncServiceProvider));
        }


        public bool IsPackageManagerUISupported(WorkspaceVisualNodeBase workspaceVisualNodeBase)
        {
            if (workspaceVisualNodeBase is null)
            {
                return false;
            }
            return IsPackageManagerUISupportAvailable;
        }
        public void OpenPackageManagerUI(WorkspaceVisualNodeBase workspaceVisualNodeBase)
        {
            _joinableTaskFactory.RunAsync(() => OpenPackageManagerUIAsync(workspaceVisualNodeBase));
        }

        private Task OpenPackageManagerUIAsync(WorkspaceVisualNodeBase workspaceVisualNodeBase)
        {
            return Task.CompletedTask;
        }
    }
}
