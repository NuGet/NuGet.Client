// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class HyperlinkClickedTelemetryEvent : TelemetryEvent
    {
        internal const string HyperlinkClickedEventName = "HyperlinkClicked";
        internal const string AlternativePackageIdPropertyName = "AlternativePackageId";
        internal const string HyperLinkTypePropertyName = "HyperlinkType";
        internal const string CurrentTabPropertyName = "CurrentTab";
        internal const string IsSolutionViewPropertyName = "IsSolutionView";

        public HyperlinkClickedTelemetryEvent(HyperlinkType hyperlinkType, ItemFilter currentTab, bool isSolutionView)
            : base(HyperlinkClickedEventName)
        {
            base[HyperLinkTypePropertyName] = hyperlinkType;
            base[CurrentTabPropertyName] = currentTab;
            base[IsSolutionViewPropertyName] = isSolutionView;
        }

        public HyperlinkClickedTelemetryEvent(HyperlinkType hyperlinkType, ItemFilter currentTab, bool isSolutionView, string alternativePackageId)
            : this(hyperlinkType, currentTab, isSolutionView)
        {
            AddPiiData(AlternativePackageIdPropertyName, VSTelemetryServiceUtility.NormalizePackageId(alternativePackageId));
        }
    }
}
