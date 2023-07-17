// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using NuGet.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio;
using System.Threading.Tasks;
using System.Threading;

namespace NuGet.SolutionRestoreManager
{
    [Export(typeof(IInfoBarService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class InfoBarService : IInfoBarService, IVsInfoBarUIEvents
    {
        IAsyncServiceProvider _asyncServiceProvider = AsyncServiceProvider.GlobalProvider;
        private IVsInfoBarUIElement _infoBarUIElement;
        private bool _visible;
        private bool _closed = false;
        private bool _closeFromHide = false;
        private uint? _eventCookie;

        [Import]
        private Lazy<IPMUIStarter> PackageManagerUIStarter { get; set; }

        public async Task ShowInfoBar(CancellationToken cancellationToken)
        {
            await ShowInfoBarAsync(cancellationToken);
        }

        public async Task HideInfoBar(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!_visible)
            {
                return;
            }

            _closeFromHide = true;

            try
            {
                _infoBarUIElement?.Close();
                _infoBarUIElement = null;
            }
            finally
            {
                _closeFromHide = false;
            }
        }

        protected async Task<IVsInfoBarHost> GetInfoBarHostAsync(CancellationToken cancellationToken)
        {
            var uiShell = await _asyncServiceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>(throwOnFailure: false);
            if (uiShell == null)
            {
                return null;
            }

            // Ensure that we are on the UI thread before interacting with the Solution Explorer UI Element
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (ErrorHandler.Failed(uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst, VSConstants.StandardToolWindows.SolutionExplorer, out var windowFrame)))
            {
                return null;
            }

            object tempObject;
            if (ErrorHandler.Failed(windowFrame.GetProperty((int)__VSFPROPID7.VSFPROPID_InfoBarHost, out tempObject)))
            {
                return null;
            }

            return tempObject as IVsInfoBarHost;
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

        internal async Task ShowInfoBarAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_closed || _visible)
            {
                return;
            }

            try
            {
                var infoBarFactory = await _asyncServiceProvider.GetServiceAsync<SVsInfoBarUIFactory, IVsInfoBarUIFactory>(throwOnFailure: false);
                if (infoBarFactory == null)
                {
                    return;
                }

                var infoBarHost = await GetInfoBarHostAsync(cancellationToken);
                if (infoBarHost == null)
                {
                    return;
                }

                // Ensure that we are on the UI thread before interacting with the UI
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var infoBarModel = GetInfoBarModel();

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
            PackageManagerUIStarter.Value.PMUIStarter();
        }
    }
}
