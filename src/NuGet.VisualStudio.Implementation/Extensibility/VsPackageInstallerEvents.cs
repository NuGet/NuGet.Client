// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using NuGet.ProjectManagement;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstallerEvents))]
    [Export(typeof(VsPackageInstallerEvents))]
    public class VsPackageInstallerEvents : IVsPackageInstallerEvents
    {
        public event VsPackageEventHandler PackageInstalled;

        public event VsPackageEventHandler PackageUninstalling;

        public event VsPackageEventHandler PackageInstalling;

        public event VsPackageEventHandler PackageUninstalled;

        public event VsPackageEventHandler PackageReferenceAdded;

        public event VsPackageEventHandler PackageReferenceRemoved;

        private readonly PackageEvents _eventSource;

        [ImportingConstructor]
        public VsPackageInstallerEvents(IPackageEventsProvider eventProvider)
        {
            _eventSource = eventProvider.GetPackageEvents();

            _eventSource.PackageInstalled += Source_PackageInstalled;
            _eventSource.PackageInstalling += Source_PackageInstalling;
            _eventSource.PackageReferenceAdded += Source_PackageReferenceAdded;
            _eventSource.PackageReferenceRemoved += Source_PackageReferenceRemoved;
            _eventSource.PackageUninstalled += Source_PackageUninstalled;
            _eventSource.PackageUninstalling += Source_PackageUninstalling;
        }

        // TODO: If the extra metadata fields are needed use: PackageManagementHelpers.CreateMetadata()

        private void Source_PackageUninstalling(object sender, PackageEventArgs e)
        {
            NotifyUninstalling(new PackageOperationEventArgs(e.InstallPath, e.Identity, null));
        }

        private void Source_PackageUninstalled(object sender, PackageEventArgs e)
        {
            NotifyUninstalled(new PackageOperationEventArgs(e.InstallPath, e.Identity, null));
        }

        private void Source_PackageReferenceRemoved(object sender, PackageEventArgs e)
        {
            NotifyReferenceRemoved(new PackageOperationEventArgs(e.InstallPath, e.Identity, null));
        }

        private void Source_PackageReferenceAdded(object sender, PackageEventArgs e)
        {
            NotifyReferenceAdded(new PackageOperationEventArgs(e.InstallPath, e.Identity, null));
        }

        private void Source_PackageInstalling(object sender, PackageEventArgs e)
        {
            NotifyInstalling(new PackageOperationEventArgs(e.InstallPath, e.Identity, null));
        }

        private void Source_PackageInstalled(object sender, PackageEventArgs e)
        {
            NotifyInstalled(new PackageOperationEventArgs(e.InstallPath, e.Identity, null));
        }

        internal void NotifyInstalling(PackageOperationEventArgs e)
        {
            if (PackageInstalling != null)
            {
                PackageInstalling(new VsPackageMetadata(e.Package, e.InstallPath));
            }
        }

        internal void NotifyInstalled(PackageOperationEventArgs e)
        {
            if (PackageInstalled != null)
            {
                PackageInstalled(new VsPackageMetadata(e.Package, e.InstallPath));
            }
        }

        internal void NotifyUninstalling(PackageOperationEventArgs e)
        {
            if (PackageUninstalling != null)
            {
                PackageUninstalling(new VsPackageMetadata(e.Package, e.InstallPath));
            }
        }

        internal void NotifyUninstalled(PackageOperationEventArgs e)
        {
            if (PackageUninstalled != null)
            {
                PackageUninstalled(new VsPackageMetadata(e.Package, e.InstallPath));
            }
        }

        internal void NotifyReferenceAdded(PackageOperationEventArgs e)
        {
            if (PackageReferenceAdded != null)
            {
                PackageReferenceAdded(new VsPackageMetadata(e.Package, e.InstallPath));
            }
        }

        internal void NotifyReferenceRemoved(PackageOperationEventArgs e)
        {
            if (PackageReferenceRemoved != null)
            {
                PackageReferenceRemoved(new VsPackageMetadata(e.Package, e.InstallPath));
            }
        }
    }
}
