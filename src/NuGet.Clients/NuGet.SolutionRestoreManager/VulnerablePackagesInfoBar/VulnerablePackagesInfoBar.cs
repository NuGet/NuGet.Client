using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#nullable enable

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Manages the display and event handling for the InfoBar related to vulnerable packages.
    /// </summary>
    internal class VulnerablePackagesInfoBar : IInfoBarPresenter
    {
        private readonly IInfoBarPresenter _infoBarPresenter;
        private readonly VulnerablePackagesInfoBarEventHandler _infoBarEventHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="VulnerablePackagesInfoBar"/> class.
        /// </summary>
        /// <param name="infoBarPresenter">The underlying presenter responsible for showing and hiding the InfoBar.</param>
        /// <param name="infoBarUIElement">The UI element representing the info bar.</param>
        internal VulnerablePackagesInfoBar(IInfoBarPresenter infoBarPresenter, IVsInfoBarUIElement infoBarUIElement)
        {
            _infoBarPresenter = infoBarPresenter ?? throw new ArgumentNullException(nameof(infoBarPresenter));
            _infoBarEventHandler = new VulnerablePackagesInfoBarEventHandler(infoBarUIElement);
        }

        /// <summary>
        /// Displays the InfoBar to the user.
        /// </summary>
        public void ShowInfoBar()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _infoBarEventHandler.AdviseEvents();
            _infoBarPresenter.ShowInfoBar();
        }

        /// <summary>
        /// Hides the InfoBar from the user.
        /// </summary>
        public void HideInfoBar()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _infoBarEventHandler.UnadviseEvents();
            _infoBarPresenter.HideInfoBar();
        }
    }
}
