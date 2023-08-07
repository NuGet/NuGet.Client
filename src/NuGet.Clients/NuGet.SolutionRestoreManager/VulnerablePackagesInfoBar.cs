// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;

#nullable enable

namespace NuGet.SolutionRestoreManager
{
    [Export(typeof(IVulnerabilitiesFoundService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VulnerablePackagesInfoBar : IVulnerabilitiesFoundService, IVsInfoBarUIEvents
    {
        IAsyncServiceProvider _asyncServiceProvider = AsyncServiceProvider.GlobalProvider;
        private IVsInfoBarUIElement? _infoBarUIElement;
        private bool _isVulnerablePackagesInfoBarVisible; // InfoBar is currently being displayed in the Solution Explorer
        private bool _wasVulnerablePackagesInfoBarClosed = false; // InfoBar was closed by the user, using the 'x'(close) in the InfoBar
        private bool _wasVulnerablePackagesInfoBarHide = false; // InfoBar was hid, this is caused because there are no more vulnerabilities to address
        private uint? _eventCookie; // To hold the connection cookie

        [Import]
        private Lazy<IPackageManagerLaunchService>? PackageManagerUIStarter { get; set; }

        public async Task HasAnyVulnerabilitiesInSolution(bool hasVulnerabilitiesInSolution, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // If the InfoBar was closed, don't show it for the rest of the VS session
            // if the infobar is already visible, no work needed
            if (_wasVulnerablePackagesInfoBarClosed || _isVulnerablePackagesInfoBarVisible)
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Hide the InfoBar if Vulnerabilities were fixed
            if (!hasVulnerabilitiesInSolution)
            {
                _infoBarUIElement?.Close(); // This is going to call OnClosed
                _wasVulnerablePackagesInfoBarHide = false;
                return;
            }

            // Initialize the InfoBar host in the SolutionExplorer window
            IVsInfoBarHost? infoBarHost;
            try
            {
                IVsUIShell? uiShell = await _asyncServiceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>(throwOnFailure: false);
                if (ErrorHandler.Failed(uiShell!.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst, VSConstants.StandardToolWindows.SolutionExplorer, out var windowFrame)))
                {
                    NullReferenceException exception = new NullReferenceException(nameof(windowFrame));
                    await TelemetryUtility.PostFaultAsync(exception, nameof(VulnerablePackagesInfoBar));
                    return;
                }

                object tempObject;
                if (ErrorHandler.Failed(windowFrame.GetProperty((int)__VSFPROPID7.VSFPROPID_InfoBarHost, out tempObject)))
                {
                    NullReferenceException exception = new NullReferenceException(nameof(tempObject));
                    await TelemetryUtility.PostFaultAsync(exception, nameof(VulnerablePackagesInfoBar));
                    return;
                }

                infoBarHost = (IVsInfoBarHost)tempObject;
            }
            catch (Exception ex)
            {
                await TelemetryUtility.PostFaultAsync(ex, nameof(VulnerablePackagesInfoBar));
                return;
            }

            // Create the VulnerabilitiesFound InfoBar
            try
            {
                IVsInfoBarUIFactory? infoBarFactory = await _asyncServiceProvider.GetServiceAsync<SVsInfoBarUIFactory, IVsInfoBarUIFactory>(throwOnFailure: false);
                if (infoBarFactory == null)
                {
                    NullReferenceException exception = new NullReferenceException(nameof(infoBarFactory));
                    await TelemetryUtility.PostFaultAsync(exception, nameof(VulnerablePackagesInfoBar));
                    return;
                }

                InfoBarModel infoBarModel = GetInfoBarModel();

                _infoBarUIElement = infoBarFactory.CreateInfoBar(infoBarModel);
                _infoBarUIElement.Advise(this, out uint cookie);
                _eventCookie = cookie;

                infoBarHost.AddInfoBar(_infoBarUIElement);

                _isVulnerablePackagesInfoBarVisible = true;
            }
            catch (Exception ex)
            {
                await TelemetryUtility.PostFaultAsync(ex, nameof(VulnerablePackagesInfoBar));
                return;
            }
        }

        public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_eventCookie.HasValue)
            {
                infoBarUIElement?.Unadvise(_eventCookie.Value);
                infoBarUIElement?.Close();
                _eventCookie = null;
            }

            _isVulnerablePackagesInfoBarVisible = false;

            if (!_wasVulnerablePackagesInfoBarHide)
            {
                _wasVulnerablePackagesInfoBarClosed = true;
            }
        }

        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            PackageManagerUIStarter?.Value.LaunchSolutionPackageManager();
        }

        protected InfoBarModel GetInfoBarModel()
        {
            return new InfoBarModel(
                Resources.InfoBar_TextMessage,
                new IVsInfoBarActionItem[] {
                    new InfoBarHyperlink(Resources.InfoBar_HyperlinkMessage),
                },
                KnownMonikers.StatusWarning);
        }
    }
}
