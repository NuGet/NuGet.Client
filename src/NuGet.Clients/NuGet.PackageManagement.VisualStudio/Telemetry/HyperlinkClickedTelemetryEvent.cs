// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class HyperlinkClickedTelemetryEvent : TelemetryEvent
    {
        internal const string HyperlinkClickedEventName = "HyperlinkClicked";
        internal const string SearchQueryPropertyName = "SearchQuery";
        internal const string HyperLinkTypePropertyName = "HyperlinkType";
        internal const string CurrentTabPropertyName = "CurrentTab";
        internal const string IsSolutionViewPropertyName = "IsSolutionView";

        public HyperlinkClickedTelemetryEvent(HyperlinkType hyperlinkType, ItemFilter currentTab, bool isSolution)
            : base(HyperlinkClickedEventName)
        {
            base[HyperLinkTypePropertyName] = hyperlinkType;
            base[CurrentTabPropertyName] = currentTab;
            base[IsSolutionViewPropertyName] = isSolution;
        }

        public HyperlinkClickedTelemetryEvent(HyperlinkType hyperlinkType, ItemFilter currentTab, bool isSolution, string searchQuery)
            : this(hyperlinkType, currentTab, isSolution)
        {
            AddPiiData(SearchQueryPropertyName, searchQuery);
        }
    }
}
