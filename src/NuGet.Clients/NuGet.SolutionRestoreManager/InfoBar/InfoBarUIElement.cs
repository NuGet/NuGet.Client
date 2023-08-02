using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#nullable enable

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Helper class for creating UI elements for info bars in Visual Studio.
    /// </summary>
    internal static class InfoBarUIElement
    {
        /// <summary>
        /// Creates a UI element for the specified info bar asynchronously.
        /// </summary>
        /// <param name="serviceProvider">Service provider to retrieve required services.</param>
        /// <param name="infoBar">The info bar to be represented as a UI element.</param>
        /// <returns>A task with the created <see cref="IVsInfoBarUIElement"/> or null if creation fails.</returns>
        internal async static Task<IVsInfoBarUIElement?> CreateAsync(IAsyncServiceProvider serviceProvider, IVsInfoBar infoBar)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IVsInfoBarUIFactory? infoBarUIFactory = await serviceProvider.GetServiceAsync<SVsInfoBarUIFactory, IVsInfoBarUIFactory>(throwOnFailure: false);
            return infoBarUIFactory?.CreateInfoBar(infoBar);
        }
    }
}
