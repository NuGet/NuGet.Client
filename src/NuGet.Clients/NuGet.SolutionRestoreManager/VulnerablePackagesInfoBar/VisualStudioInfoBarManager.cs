using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#nullable enable

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Represents the presenter for displaying info bars in Visual Studio.
    /// </summary>
    internal interface IInfoBarPresenter
    {
        /// <summary>
        /// Shows the info bar in Visual Studio.
        /// </summary>
        void ShowInfoBar();

        /// <summary>
        /// Hides the info bar in Visual Studio.
        /// </summary>
        void HideInfoBar();
    }

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

    /// <summary>
    /// Provides methods to interact with the Solution Explorer window frame.
    /// </summary>
    internal static class WindowFrame
    {
        /// <summary>
        /// Retrieves the frame for the Solution Explorer tool window asynchronously.
        /// </summary>
        /// <param name="serviceProvider">The service provider to retrieve services from.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the IVsWindowFrame of the Solution Explorer, or null if not found.</returns>
        internal async static Task<IVsWindowFrame?> GetSolutionExplorerFrameAsync(IAsyncServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IVsUIShell? uiShell = await serviceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>(throwOnFailure: false);
            if (uiShell == null)
            {
                // Consider logging the error or throwing an exception based on your requirements.
                return null;
            }
            if (ErrorHandler.Succeeded(uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst, VSConstants.StandardToolWindows.SolutionExplorer, out IVsWindowFrame frame)))
            {
                return frame;
            }
            return null;
        }
    }

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

    /// <summary>
    /// Defines methods to handle events from the InfoBar UI element and manage their subscription.
    /// </summary>
    internal interface IVsInfoBarUIEventsHandler : IVsInfoBarUIEvents
    {
        /// <summary>
        /// Subscribes to events from the InfoBar UI element.
        /// </summary>
        void AdviseEvents();

        /// <summary>
        /// Unsubscribes from events from the InfoBar UI element.
        /// </summary>
        void UnadviseEvents();
    }

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
