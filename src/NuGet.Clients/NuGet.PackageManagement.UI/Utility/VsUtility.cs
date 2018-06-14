using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.UI;

namespace NuGet.PackageManagement.UI
{
    public static class VsUtility
    {
        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD010", Justification = "NuGet/Home#4833 Baseline")]
        public static IEnumerable<IVsWindowFrame> GetDocumentWindows(IVsUIShell uiShell)
        {
            IEnumWindowFrames documentWindowEnumerator;
            var hr = uiShell.GetDocumentWindowEnum(out documentWindowEnumerator);
            if (documentWindowEnumerator == null)
            {
                yield break;
            }

            var windowFrames = new IVsWindowFrame[1];
            uint frameCount;
            while (documentWindowEnumerator.Next(1, windowFrames, out frameCount) == VSConstants.S_OK &&
                   frameCount == 1)
            {
                yield return windowFrames[0];
            }
        }

        // Gets the package manager control hosted in the window frame.
        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD010", Justification = "NuGet/Home#4833 Baseline")]
        public static PackageManagerControl GetPackageManagerControl(IVsWindowFrame windowFrame)
        {
            object property;
            var hr = windowFrame.GetProperty(
                (int)__VSFPROPID.VSFPROPID_DocView,
                out property);

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