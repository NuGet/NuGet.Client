// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using NuGet.PackageManagement;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Contains batch events to be raised when performing multiple packages install/ uninstall in a project with packages.config
    /// </summary>
    [Export(typeof(IVsPackageInstallerProjectEvents))]
    public class VsPackageInstallerProjectEvents : IVsPackageInstallerProjectEvents
    {
        public event VsPackageProjectEventHandler BatchStart;

        public event VsPackageProjectEventHandler BatchEnd;

        [ImportingConstructor]
        public VsPackageInstallerProjectEvents(IPackageProjectEventsProvider eventProvider)
        {
            var eventSource = eventProvider.GetPackageProjectEvents();

            eventSource.BatchStart += NotifyBatchStart;
            eventSource.BatchEnd += NotifyBatchEnd;
        }

        private void NotifyBatchStart(object sender, PackageProjectEventArgs e)
        {
            var handle = BatchStart;
            handle?.Invoke(new VsPackageProjectMetadata(e.Id, e.Name));
        }

        private void NotifyBatchEnd(object sender, PackageProjectEventArgs e)
        {
            var handle = BatchEnd;
            handle?.Invoke(new VsPackageProjectMetadata(e.Id, e.Name));
        }

    }
}
