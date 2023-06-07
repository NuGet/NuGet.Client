// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using ContractsItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;

namespace NuGet.PackageManagement.Telemetry
{
    public class NavigatedTelemetryEvent : TelemetryEvent
    {
        internal const string NavigatedEventName = "Navigated";
        internal const string NavigationTypePropertyName = "NavigationType";
        internal const string CurrentTabPropertyName = "CurrentTab";
        internal const string IsSolutionViewPropertyName = "IsSolutionView";

        public NavigatedTelemetryEvent(NavigationType navigationType, ContractsItemFilter currentTab, bool isSolutionView)
            : base(NavigatedEventName)
        {
            base[NavigationTypePropertyName] = navigationType;
            base[CurrentTabPropertyName] = currentTab;
            base[IsSolutionViewPropertyName] = isSolutionView;
        }
    }
}
