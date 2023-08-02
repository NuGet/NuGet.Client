using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#nullable enable

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Handles events for the InfoBar UI element.
    /// </summary>
    internal class VulnerablePackagesInfoBarEventHandler : IVsInfoBarUIEventsHandler
    {
        private readonly IVsInfoBarUIElement _infoBarUIElement;
        private uint _cookie;  // To hold the connection cookie

        /// <summary>
        /// Initializes a new instance of the <see cref="InfoBarEventHandler"/> class.
        /// </summary>
        /// <param name="infoBarUIElement">The UI element representing the info bar.</param>
        public VulnerablePackagesInfoBarEventHandler(IVsInfoBarUIElement infoBarUIElement)
        {
            _infoBarUIElement = infoBarUIElement ?? throw new ArgumentNullException(nameof(infoBarUIElement));
        }

        /// <summary>
        /// Subscribes to events from the InfoBar UI element.
        /// </summary>
        public void AdviseEvents()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _ = _infoBarUIElement.Advise(this, out _cookie);
        }

        /// <summary>
        /// Unsubscribes from events from the InfoBar UI element.
        /// </summary>
        public void UnadviseEvents()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _infoBarUIElement.Unadvise(_cookie);
            _cookie = default;
        }

        /// <inheritdoc/>
        public void OnClosed(IVsInfoBarUIElement infoBarUIElement) { }

        /// <inheritdoc/>
        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem) { }
    }
}
