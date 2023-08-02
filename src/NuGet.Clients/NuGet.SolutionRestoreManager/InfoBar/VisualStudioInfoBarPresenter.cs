// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;

#nullable enable

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Provides functionality to manage and display info bars in Visual Studio.
    /// </summary>
    internal class VisualStudioInfoBarPresenter : IInfoBarPresenter
    {
        private readonly IVsInfoBarHost _infoBarHost;
        private readonly IVsInfoBarUIElement _infoBarUIElement;

        /// <summary>
        /// Initializes a new instance of the <see cref="VisualStudioInfoBarPresenter"/> class.
        /// </summary>
        /// <param name="infoBarHost">The info bar host in Visual Studio.</param>
        /// <param name="infoBarUIElement">The UI element representing the info bar.</param>
        public VisualStudioInfoBarPresenter(IVsWindowFrame vsWindowFrame, IVsInfoBarUIElement vsInfoBarUIElement)
        {
            _infoBarUIElement = vsInfoBarUIElement ?? throw new ArgumentNullException(nameof(vsInfoBarUIElement));

            ThreadHelper.ThrowIfNotOnUIThread();
            if (!TryGetInfoBarHost(vsWindowFrame, out IVsInfoBarHost? infoBarHost))
            {
                throw new ArgumentException("Failed to get InfoBar host from the provided frame.", nameof(vsWindowFrame));
            }

            _infoBarHost = infoBarHost!;
        }

        private static bool TryGetInfoBarHost(IVsWindowFrame frame, out IVsInfoBarHost? infoBarHost)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (ErrorHandler.Failed(frame.GetProperty((int)__VSFPROPID7.VSFPROPID_InfoBarHost, out object? infoBarHostObj)))
            {
                infoBarHost = null;
                return false;
            }
            return (infoBarHost = infoBarHostObj as IVsInfoBarHost) != null;
        }

        /// <summary>
        /// Shows the info bar in Visual Studio.
        /// </summary>
        public void ShowInfoBar()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _infoBarHost.AddInfoBar(_infoBarUIElement);
        }

        /// <summary>
        /// Hides the info bar in Visual Studio.
        /// </summary>
        public void HideInfoBar()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _infoBarHost.RemoveInfoBar(_infoBarUIElement);
        }
    }
}
