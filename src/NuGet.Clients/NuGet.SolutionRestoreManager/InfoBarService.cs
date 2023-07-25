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
    [Export(typeof(IInfoBarService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class InfoBarService : IInfoBarService, IVsInfoBarUIEvents
    {
        IAsyncServiceProvider _asyncServiceProvider = AsyncServiceProvider.GlobalProvider;
        private IVsInfoBarUIElement? _infoBarUIElement;
        private bool _visible;
        private bool _closed = false;
        private bool _closeFromHide = false;
        private uint? _eventCookie;

        [Import]
        private Lazy<IPMUIStarter>? PackageManagerUIStarter { get; set; }

        public async Task ShowAsync(CancellationToken cancellationToken)
        {
            await CreateAndShowInfoBarAsync(cancellationToken);
        }

        public async Task RemoveAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!_visible)
            {
                return;
            }

            _closeFromHide = true;

            try
            {
                if (_eventCookie.HasValue)
                {
                    _infoBarUIElement?.Close();
                    _infoBarUIElement?.Unadvise(_eventCookie.Value);
                    _infoBarUIElement = null;
                }
            }
            finally
            {
                _closeFromHide = false;
            }
        }

        protected InfoBarModel GetInfoBarModel()
        {
            return new InfoBarModel(
                new IVsInfoBarTextSpan[] {
                    new InfoBarTextSpan(Resources.InfoBar_TextMessage),
                },
                new IVsInfoBarActionItem[] {
                    new InfoBarHyperlink(Resources.InfoBar_HyperlinkMessage),
                },
                KnownMonikers.StatusWarning);
        }

        internal async Task CreateAndShowInfoBarAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (_closed || _visible)
            {
                return;
            }

            IVsInfoBarHost? infoBarHost;
            try
            {
                IVsUIShell? uiShell = await _asyncServiceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>(throwOnFailure: false);
                if (uiShell == null)
                {
                    throw new NullReferenceException(nameof(uiShell));
                }

                if (ErrorHandler.Failed(uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst, VSConstants.StandardToolWindows.SolutionExplorer, out var windowFrame)))
                {
                    throw new NullReferenceException(nameof(windowFrame));
                }

                object tempObject;
                if (ErrorHandler.Failed(windowFrame.GetProperty((int)__VSFPROPID7.VSFPROPID_InfoBarHost, out tempObject)))
                {
                    throw new NullReferenceException(nameof(tempObject));
                }

                infoBarHost = (IVsInfoBarHost)tempObject;
            }
            catch (Exception ex)
            {
                await TelemetryUtility.PostFaultAsync(ex, nameof(InfoBarService));
                return;
            }

            try
            {
                IVsInfoBarUIFactory? infoBarFactory = await _asyncServiceProvider.GetServiceAsync<SVsInfoBarUIFactory, IVsInfoBarUIFactory>(throwOnFailure: false);
                if (infoBarFactory == null)
                {
                    NullReferenceException exception = new NullReferenceException(nameof(infoBarFactory));
                    await TelemetryUtility.PostFaultAsync(exception, nameof(InfoBarService));
                    return;
                }

                InfoBarModel infoBarModel = GetInfoBarModel();

                _infoBarUIElement = infoBarFactory.CreateInfoBar(infoBarModel);
                _infoBarUIElement.Advise(this, out uint cookie);
                _eventCookie = cookie;

                infoBarHost.AddInfoBar(_infoBarUIElement);

                _visible = true;
            }
            catch
            {
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

            _visible = false;

            if (!_closeFromHide)
            {
                _closed = true;
            }
        }

        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            PackageManagerUIStarter?.Value.PMUIStarter();
        }
    }
}
