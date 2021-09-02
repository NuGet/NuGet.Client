// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class HyperlinkClickedTelemetryEvent : TelemetryEvent
    {
        internal const string HyperlinkClickedEventName = "HyperlinkClicked";
        internal const string SearchQueryPropertyName = "SearchQuery";
        internal const string HyperLinkTypePropertyName = "HyperlinkType";

        public HyperlinkClickedTelemetryEvent(HyperlinkType hyperlinkType) : base(HyperlinkClickedEventName)
        {
            base[HyperLinkTypePropertyName] = hyperlinkType;
        }

        public HyperlinkClickedTelemetryEvent(HyperlinkType hyperlinkType, string searchQuery) : this(hyperlinkType)
        {
            AddPiiData(SearchQueryPropertyName, searchQuery);
        }
    }
}
