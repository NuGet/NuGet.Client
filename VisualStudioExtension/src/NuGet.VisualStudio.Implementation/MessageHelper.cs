// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.VisualStudio
{
    public static class MessageHelper
    {
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
        public static void ShowWarningMessage(string message, string title)
        {
            VsShellUtilities.ShowMessageBox(
                ServiceLocator.GetInstance<IServiceProvider>(),
                message,
                title,
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
        public static void ShowInfoMessage(string message, string title)
        {
            VsShellUtilities.ShowMessageBox(
                ServiceLocator.GetInstance<IServiceProvider>(),
                message,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public static void ShowErrorMessage(Exception exception, string title)
        {
            ShowErrorMessage(ExceptionUtility.Unwrap(exception).Message, title);
        }

        public static void ShowErrorMessage(string message, string title)
        {
            VsShellUtilities.ShowMessageBox(
                ServiceLocator.GetInstance<IServiceProvider>(),
                message,
                title,
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public static bool? ShowQueryMessage(string message, string title, bool showCancelButton)
        {
            int result = VsShellUtilities.ShowMessageBox(
                ServiceLocator.GetInstance<IServiceProvider>(),
                message,
                title,
                OLEMSGICON.OLEMSGICON_QUERY,
                showCancelButton ? OLEMSGBUTTON.OLEMSGBUTTON_YESNOCANCEL : OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            if (result == NativeMethods.IDCANCEL)
            {
                return null;
            }
            return (result == NativeMethods.IDYES);
        }
    }
}
