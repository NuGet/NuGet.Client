using System.Collections.Generic;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.PackageManagement.UI
{
    public static class VsUtility
    {
        public static IEnumerable<IVsWindowFrame> GetDocumentWindows(IVsUIShell uiShell)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var hr = uiShell.GetDocumentWindowEnum(out var documentWindowEnumerator);
            if (documentWindowEnumerator == null)
            {
                yield break;
            }

            var windowFrames = new IVsWindowFrame[1];
            while (documentWindowEnumerator.Next(1, windowFrames, out var frameCount) == VSConstants.S_OK &&
                   frameCount == 1)
            {
                yield return windowFrames[0];
            }
        }

        // Gets the package manager control hosted in the window frame.
        public static PackageManagerControl GetPackageManagerControl(IVsWindowFrame windowFrame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var hr = windowFrame.GetProperty(
                (int)__VSFPROPID.VSFPROPID_DocView,
                out var property);

            var windowPane = property as PackageManagerWindowPane;
            if (windowPane == null)
            {
                return null;
            }

            var packageManagerControl = windowPane.Content as PackageManagerControl;
            return packageManagerControl;
        }
    }
}
