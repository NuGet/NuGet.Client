using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.VisualStudio
{
    public static class MessageHelper
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
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

        public static class ExceptionUtility
        {
            public static Exception Unwrap(Exception exception)
            {
                if (exception == null)
                {
                    throw new ArgumentNullException("exception");
                }

                if (exception.InnerException == null)
                {
                    return exception;
                }

                // Always return the inner exception from a target invocation exception
                if (exception is AggregateException ||
                    exception is TargetInvocationException)
                {
                    return exception.GetBaseException();
                }

                return exception;
            }
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
            else
            {
                return (result == NativeMethods.IDYES);
            }
        }

        internal class NativeMethods
        {
            public const int IDCANCEL = 2;
            public const int IDYES = 6;
            public const int IDNO = 7;
        }
    }
}