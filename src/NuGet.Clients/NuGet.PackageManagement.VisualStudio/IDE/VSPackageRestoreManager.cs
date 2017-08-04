// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IPackageRestoreManager))]
    internal sealed class VSPackageRestoreManager : PackageRestoreManager
    {
        private ISolutionManager SolutionManager { get; }

        [ImportingConstructor]
        public VSPackageRestoreManager(
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            ISolutionManager solutionManager)
            : base(sourceRepositoryProvider, settings, solutionManager)
        {
            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            SolutionManager = solutionManager;
            SolutionManager.NuGetProjectAdded += OnNuGetProjectAdded;
            SolutionManager.SolutionOpened += OnSolutionOpenedOrClosed;
            SolutionManager.SolutionClosed += OnSolutionOpenedOrClosed;
        }

        private void OnSolutionOpenedOrClosed(object sender, EventArgs e)
        {
            // This is a solution event. Should be on the UI thread
            ThreadHelper.ThrowIfNotOnUIThread();

            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    // We need to do the check even on Solution Closed because, let's say if the yellow Update bar
                    // is showing and the user closes the solution; in that case, we want to hide the Update bar.
                    var solutionDirectory = SolutionManager.SolutionDirectory;
                    await RaisePackagesMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
                });
        }

        private void OnNuGetProjectAdded(object sender, NuGetProjectEventArgs e)
        {
            // This is a solution event. Should be on the UI thread
            ThreadHelper.ThrowIfNotOnUIThread();

            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    var solutionDirectory = SolutionManager.SolutionDirectory;
                    await RaisePackagesMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
                });
        }
    }
}
