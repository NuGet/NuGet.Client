// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using NuGet.PackageManagement;
using NuGet.VisualStudio.Etw;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    /// <summary>
    /// Contains batch events to be raised when performing multiple packages install/ uninstall in a project with packages.config
    /// </summary>
    [Export(typeof(IVsPackageInstallerProjectEvents))]
    public class VsPackageInstallerProjectEvents : IVsPackageInstallerProjectEvents
    {
        [ImportingConstructor]
        public VsPackageInstallerProjectEvents(INuGetTelemetryProvider telemetryProvider)
        {
            // MEF components do not participate in Visual Studio's Package extensibility,
            // hence importing INuGetTelemetryProvider ensures that the ETW collector is
            // set up correctly.
        }

        public event VsPackageProjectEventHandler _batchStart;
        const string BatchStartEventName = nameof(IVsPackageInstallerProjectEvents) + "." + nameof(BatchStart);
        public event VsPackageProjectEventHandler BatchStart
        {
            add
            {
                NuGetExtensibilityEtw.EventSource.Write(BatchStartEventName, NuGetExtensibilityEtw.AddEventOptions);
                _batchStart += value;
            }
            remove
            {
                NuGetExtensibilityEtw.EventSource.Write(BatchStartEventName, NuGetExtensibilityEtw.RemoveEventOptions);
                _batchStart -= value;
            }
        }

        public event VsPackageProjectEventHandler _batchEnd;
        const string BatchEndEventName = nameof(IVsPackageInstallerProjectEvents) + "." + nameof(BatchEnd);
        public event VsPackageProjectEventHandler BatchEnd
        {
            add
            {
                NuGetExtensibilityEtw.EventSource.Write(BatchEndEventName, NuGetExtensibilityEtw.AddEventOptions);
                _batchEnd += value;
            }
            remove
            {
                NuGetExtensibilityEtw.EventSource.Write(BatchEndEventName, NuGetExtensibilityEtw.RemoveEventOptions);
                _batchEnd -= value;
            }
        }

        [ImportingConstructor]
        public VsPackageInstallerProjectEvents(IPackageProjectEventsProvider eventProvider)
        {
            var eventSource = eventProvider.GetPackageProjectEvents();

            eventSource.BatchStart += NotifyBatchStart;
            eventSource.BatchEnd += NotifyBatchEnd;
        }

        private void NotifyDelegates(PackageProjectEventArgs e, Delegate[] delegates)
        {
            VsPackageProjectMetadata eventData = new(e.Id, e.Name);
            for (int i = 0; i < delegates.Length; i++)
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    var handler = (VsPackageProjectEventHandler)(delegates[i]);
                    handler(eventData);
                }
                catch
                {
                    // someone else's code threw an exception, but we should keep notifying other event subscribers
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }
        }

        private void NotifyBatchStart(object sender, PackageProjectEventArgs e)
        {
            if (_batchStart != null)
            {
                NotifyDelegates(e, _batchStart.GetInvocationList());
            }
        }

        private void NotifyBatchEnd(object sender, PackageProjectEventArgs e)
        {
            if (_batchEnd != null)
            {
                NotifyDelegates(e, _batchEnd.GetInvocationList());
            }
        }

    }
}
