// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using NuGet.ProjectManagement;
using NuGet.VisualStudio.Etw;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    [Export(typeof(IVsPackageInstallerEvents))]
    [Export(typeof(VsPackageInstallerEvents))]
    public class VsPackageInstallerEvents : IVsPackageInstallerEvents
    {
        private event VsPackageEventHandler _packageInstalled;
        const string VsPackageInstalledEventName = nameof(IVsPackageInstallerEvents) + "." + nameof(PackageInstalled);
        public event VsPackageEventHandler PackageInstalled
        {
            add
            {
                NuGetExtensibilityEtw.EventSource.Write(VsPackageInstalledEventName, NuGetExtensibilityEtw.AddEventOptions);
                _packageInstalled += value;
            }
            remove
            {
                NuGetExtensibilityEtw.EventSource.Write(VsPackageInstalledEventName, NuGetExtensibilityEtw.RemoveEventOptions);
                _packageInstalled -= value;
            }
        }

        public event VsPackageEventHandler _packageUninstalling;
        const string PackageUninstallingEventName = nameof(IVsPackageInstallerEvents) + "." + nameof(PackageInstalled);
        public event VsPackageEventHandler PackageUninstalling
        {
            add
            {
                NuGetExtensibilityEtw.EventSource.Write(PackageUninstallingEventName, NuGetExtensibilityEtw.AddEventOptions);
                _packageUninstalling += value;
            }
            remove
            {
                NuGetExtensibilityEtw.EventSource.Write(PackageUninstallingEventName, NuGetExtensibilityEtw.RemoveEventOptions);
                _packageUninstalling -= value;
            }
        }

        public event VsPackageEventHandler _packageInstalling;
        const string PackageInstallingEventName = nameof(IVsPackageInstallerEvents) + "." + nameof(PackageInstalled);
        public event VsPackageEventHandler PackageInstalling
        {
            add
            {
                NuGetExtensibilityEtw.EventSource.Write(PackageInstallingEventName, NuGetExtensibilityEtw.AddEventOptions);
                _packageInstalling += value;
            }
            remove
            {
                NuGetExtensibilityEtw.EventSource.Write(PackageInstallingEventName, NuGetExtensibilityEtw.RemoveEventOptions);
                _packageInstalling -= value;
            }
        }

        public event VsPackageEventHandler _packageUninstalled;
        const string PackageUninstalledEvnentName = nameof(IVsPackageInstallerEvents) + "." + nameof(PackageUninstalled);
        public event VsPackageEventHandler PackageUninstalled
        {
            add
            {
                NuGetExtensibilityEtw.EventSource.Write(PackageUninstalledEvnentName, NuGetExtensibilityEtw.AddEventOptions);
                _packageUninstalled += value;
            }
            remove
            {
                NuGetExtensibilityEtw.EventSource.Write(PackageUninstalledEvnentName, NuGetExtensibilityEtw.RemoveEventOptions);
                _packageUninstalled -= value;
            }
        }

        public event VsPackageEventHandler _packageReferenceAdded;
        const string PackageReferenceAddedEventName = nameof(IVsPackageInstallerEvents) + "." + nameof(PackageReferenceAdded);
        public event VsPackageEventHandler PackageReferenceAdded
        {
            add
            {
                NuGetExtensibilityEtw.EventSource.Write(PackageReferenceAddedEventName, NuGetExtensibilityEtw.AddEventOptions);
                _packageReferenceAdded += value;
            }
            remove
            {
                NuGetExtensibilityEtw.EventSource.Write(PackageReferenceAddedEventName, NuGetExtensibilityEtw.RemoveEventOptions);
                _packageReferenceAdded -= value;
            }
        }

        public event VsPackageEventHandler _packageReferenceRemoved;
        const string PackageReferenceRemovedEventName = nameof(IVsPackageInstallerEvents) + "." + nameof(PackageReferenceRemoved);
        public event VsPackageEventHandler PackageReferenceRemoved
        {
            add
            {
                NuGetExtensibilityEtw.EventSource.Write(PackageReferenceRemovedEventName, NuGetExtensibilityEtw.AddEventOptions);
                _packageReferenceRemoved += value;
            }
            remove
            {
                NuGetExtensibilityEtw.EventSource.Write(PackageReferenceRemovedEventName, NuGetExtensibilityEtw.RemoveEventOptions);
                _packageReferenceRemoved -= value;
            }
        }

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

        private void NotifyDelegates(PackageOperationEventArgs e, Delegate[] delegates)
        {
            VsPackageMetadata eventData = new(e.Package, e.InstallPath);
            for (int i = 0; i < delegates.Length; i++)
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    var handler = (VsPackageEventHandler)(delegates[i]);
                    handler(eventData);
                }
                catch
                {
                    // someone else's code threw an exception, but we should keep notifying other event subscribers
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }
        }

        internal void NotifyInstalling(PackageOperationEventArgs e)
        {
            if (_packageInstalling != null)
            {
                NotifyDelegates(e, _packageInstalling.GetInvocationList());
            }
        }

        internal void NotifyInstalled(PackageOperationEventArgs e)
        {
            if (_packageInstalled != null)
            {
                NotifyDelegates(e, _packageInstalled.GetInvocationList());
            }
        }

        internal void NotifyUninstalling(PackageOperationEventArgs e)
        {
            if (_packageUninstalling != null)
            {
                NotifyDelegates(e, _packageUninstalling.GetInvocationList());
            }
        }

        internal void NotifyUninstalled(PackageOperationEventArgs e)
        {
            if (_packageUninstalled != null)
            {
                NotifyDelegates(e, _packageUninstalled.GetInvocationList());
            }
        }

        internal void NotifyReferenceAdded(PackageOperationEventArgs e)
        {
            if (_packageReferenceAdded != null)
            {
                NotifyDelegates(e, _packageReferenceAdded.GetInvocationList());
            }
        }

        internal void NotifyReferenceRemoved(PackageOperationEventArgs e)
        {
            if (_packageReferenceRemoved != null)
            {
                NotifyDelegates(e, _packageReferenceRemoved.GetInvocationList());
            }
        }
    }
}
