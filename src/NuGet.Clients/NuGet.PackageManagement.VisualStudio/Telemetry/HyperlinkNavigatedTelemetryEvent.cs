// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using ContractsItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class HyperlinkNavigatedTelemetryEvent : NavigatedTelemetryEvent
    {
        internal const string AlternativePackageIdPropertyName = "AlternativePackageId";
        internal const string HyperLinkTypePropertyName = "HyperlinkType";

        public HyperlinkNavigatedTelemetryEvent(HyperlinkType hyperlinkType, ContractsItemFilter currentTab, bool isSolutionView)
            : base(NavigationType.Hyperlink, currentTab, isSolutionView)
        {
            base[HyperLinkTypePropertyName] = hyperlinkType;
        }

        public HyperlinkNavigatedTelemetryEvent(HyperlinkType hyperlinkType, ContractsItemFilter currentTab, bool isSolutionView, string alternativePackageId)
            : this(hyperlinkType, currentTab, isSolutionView)
        {
            AddPiiData(AlternativePackageIdPropertyName, VSTelemetryServiceUtility.NormalizePackageId(alternativePackageId));
        }
    }
}
