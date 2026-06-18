// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Manages VS InfoBars displayed at the top of a Package Manager UI window frame.
    /// </summary>
    internal sealed class PackageManagerInfoBarService : IVsInfoBarUIEvents, IDisposable
    {
        private readonly IVsInfoBarHost _infoBarHost;
        private readonly IVsInfoBarUIFactory _infoBarFactory;
        private readonly Dictionary<string, InfoBarEntry> _activeInfoBars = new();
        private bool _disposed;

        private PackageManagerInfoBarService(IVsInfoBarHost infoBarHost, IVsInfoBarUIFactory infoBarFactory)
        {
            _infoBarHost = infoBarHost;
            _infoBarFactory = infoBarFactory;
        }

        /// <summary>
        /// Creates a <see cref="PackageManagerInfoBarService"/> for the given window frame, or returns null if the frame doesn't support InfoBars.
        /// </summary>
        public static PackageManagerInfoBarService? TryCreate(IVsWindowFrame windowFrame, IVsInfoBarUIFactory infoBarFactory)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (windowFrame == null || infoBarFactory == null)
            {
                return null;
            }

            int hr = windowFrame.GetProperty((int)__VSFPROPID7.VSFPROPID_InfoBarHost, out object hostObj);
            if (ErrorHandler.Failed(hr) || hostObj is not IVsInfoBarHost infoBarHost)
            {
                return null;
            }

            return new PackageManagerInfoBarService(infoBarHost, infoBarFactory);
        }

        /// <summary>
        /// Shows an InfoBar with the given key. If an InfoBar with that key is already visible, it is not duplicated.
        /// </summary>
        public void ShowInfoBar(string key, InfoBarModel model)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_disposed || _activeInfoBars.ContainsKey(key))
            {
                return;
            }

            IVsInfoBarUIElement uiElement = _infoBarFactory.CreateInfoBar(model);
            if (uiElement == null)
            {
                return;
            }

            uiElement.Advise(this, out uint cookie);
            _infoBarHost.AddInfoBar(uiElement);

            _activeInfoBars[key] = new InfoBarEntry(uiElement, cookie);
        }

        /// <summary>
        /// Hides and removes the InfoBar with the given key.
        /// </summary>
        public void HideInfoBar(string key)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_activeInfoBars.TryGetValue(key, out InfoBarEntry entry))
            {
                entry.UIElement.Unadvise(entry.Cookie);
                entry.UIElement.Close();
                _activeInfoBars.Remove(key);
            }
        }

        /// <summary>
        /// Returns whether an InfoBar with the given key is currently displayed.
        /// </summary>
        public bool IsInfoBarVisible(string key) => _activeInfoBars.ContainsKey(key);

        void IVsInfoBarUIEvents.OnClosed(IVsInfoBarUIElement infoBarUIElement)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Find and remove the closed InfoBar from our tracking dictionary
            string? keyToRemove = null;
            foreach (KeyValuePair<string, InfoBarEntry> kvp in _activeInfoBars)
            {
                if (kvp.Value.UIElement == infoBarUIElement)
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }

            if (keyToRemove != null)
            {
                _activeInfoBars[keyToRemove].UIElement.Unadvise(_activeInfoBars[keyToRemove].Cookie);
                _activeInfoBars.Remove(keyToRemove);
            }
        }

        void IVsInfoBarUIEvents.OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Action routing will be handled by derived scenarios or event handlers.
            // For now, this is a placeholder for future action routing.
            ActionItemClicked?.Invoke(this, new InfoBarActionClickedEventArgs(infoBarUIElement, actionItem));
        }

        /// <summary>
        /// Raised when a user clicks an action item (hyperlink/button) in any managed InfoBar.
        /// </summary>
        internal event EventHandler<InfoBarActionClickedEventArgs>? ActionItemClicked;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // InfoBar cleanup requires the UI thread. If called off-thread (rare), skip cleanup —
            // the window frame closing will release the InfoBars.
            if (!ThreadHelper.CheckAccess())
            {
                return;
            }

#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread — guarded by CheckAccess() above
            foreach (InfoBarEntry entry in _activeInfoBars.Values)
            {
                entry.UIElement.Unadvise(entry.Cookie);
                entry.UIElement.Close();
            }
#pragma warning restore VSTHRD010

            _activeInfoBars.Clear();
        }

        private sealed class InfoBarEntry
        {
            public InfoBarEntry(IVsInfoBarUIElement uiElement, uint cookie)
            {
                UIElement = uiElement;
                Cookie = cookie;
            }

            public IVsInfoBarUIElement UIElement { get; }
            public uint Cookie { get; }
        }
    }

    internal sealed class InfoBarActionClickedEventArgs : EventArgs
    {
        public InfoBarActionClickedEventArgs(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
        {
            InfoBarUIElement = infoBarUIElement;
            ActionItem = actionItem;
        }

        public IVsInfoBarUIElement InfoBarUIElement { get; }
        public IVsInfoBarActionItem ActionItem { get; }
    }
}
