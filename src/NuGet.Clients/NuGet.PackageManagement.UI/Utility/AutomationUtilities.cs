// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows.Automation.Peers;
using Microsoft.Internal.VisualStudio.PlatformUI.Automation;
using Microsoft.Internal.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI.Utility
{
    // disambiguate from Microsoft.Internal.VisualStudio.PlatformUI.UIElement
    using UIElement = System.Windows.UIElement;

    public static class AutomationUtilities
    {
        const AutomationEvents UndefinedEvent = (AutomationEvents)(-1);
        static readonly AutomationEvents LiveRegionChangedEvent;

        static AutomationUtilities()
        {
            // see if the version of WPF we're running with defines AutomationEvents.LiveRegionChanged
            if (!Enum.TryParse("LiveRegionChanged", out LiveRegionChangedEvent))
                LiveRegionChangedEvent = UndefinedEvent;
        }

        /// <summary>
        /// Indicates whether WPF natively supports live regions
        /// </summary>
        internal static bool WpfSupportsLiveRegions => (LiveRegionChangedEvent != UndefinedEvent);

        /// <summary>
        /// Raises a <see cref="LiveRegionChangedEvent"/> for <paramref name="element"/>.
        /// </summary>
        /// <param name="element">The element to raise the event for</param>
        public static void RaiseLiveRegionChangedEvent(UIElement element)
        {
            Validate.IsNotNull(element, nameof(element));

            // if WPF defines LiveRegionChanged, raise the public event
            if (WpfSupportsLiveRegions)
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(element);
                peer?.RaiseAutomationEvent(LiveRegionChangedEvent);
            }

            // otherwise, raise the custom LiveRegionChanged event
            else
            {
                CustomAutomationEvents.RaiseLiveRegionChangedEvent(element);
            }
        }
    }
}
