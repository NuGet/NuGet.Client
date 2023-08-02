using Microsoft.VisualStudio.Shell.Interop;

#nullable enable

namespace NuGet.SolutionRestoreManager
{
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
}
