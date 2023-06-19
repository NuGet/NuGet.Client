// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio;

using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;

namespace NuGet.SolutionRestoreManager
{
    public abstract class BaseInfoBar : IVsInfoBarUIEvents
    {
        protected IAsyncServiceProvider _asyncServiceProvider { get; }
        private IVsInfoBarUIElement _infoBarUIElement;
        private bool _visible;
        private uint? _eventCookie;
        private bool _closeFromHide;


        internal BaseInfoBar(IAsyncServiceProvider asyncServiceProvider)
        {
            Validate.IsNotNull(asyncServiceProvider, nameof(asyncServiceProvider));

            _asyncServiceProvider = asyncServiceProvider;
        }

        protected abstract InfoBarModel GetInfoBarModel();
        protected abstract void InvokeAction(string action);

        protected virtual void OnClosed(bool closeFromHide)
        {
        }

        protected virtual async Task<IVsInfoBarHost> GetInfoBarHostAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // This is a default implementation that adds the info bar to the main window
            var shell = await _asyncServiceProvider.GetServiceAsync<SVsShell, IVsShell>(throwOnFailure: false);
            if (shell == null)
            {
                return null;
            }

            object tempObject = null;
            if (ErrorHandler.Failed(shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out tempObject)))
            {
                return null;
            }

            return tempObject as IVsInfoBarHost;
        }

        internal async Task ShowAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_visible)
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

        internal async Task HideAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Ensure that we are on the UI thread before interacting with the UI element
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
            OnClosed(_closeFromHide);
        }

        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (actionItem.ActionContext is string action)
                {
                    InvokeAction(action);
                }
            }
            catch
            {
                return;
            }
        }

    }
}
