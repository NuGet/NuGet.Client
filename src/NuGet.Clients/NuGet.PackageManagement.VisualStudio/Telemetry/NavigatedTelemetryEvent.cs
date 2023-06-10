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
        internal const string PackageSourceMappingStatusPropertyName = "PackageSourceMappingStatus";
        internal const string OriginPropertyName = "Origin";
        internal const string SourcesCountPropertyName = "SourcesCount";
        internal const string IsGlobbingPropertyName = "IsGlobbing";

        public NavigatedTelemetryEvent(NavigationType navigationType, ContractsItemFilter currentTab, bool isSolutionView, PackageSourceMappingStatus packageSourceMappingStatus)
            : base(NavigatedEventName)
        {
            base[NavigationTypePropertyName] = navigationType;
            base[CurrentTabPropertyName] = currentTab;
            base[IsSolutionViewPropertyName] = isSolutionView;
            if (packageSourceMappingStatus != PackageSourceMappingStatus.Unspecified)
            {
                base[PackageSourceMappingStatusPropertyName] = packageSourceMappingStatus;
            }
        }

        public NavigatedTelemetryEvent(NavigationType navigationType, NavigationOrigin navigationOrigin)
            : base(NavigatedEventName)
        {
            base[NavigationTypePropertyName] = navigationType;
            base[OriginPropertyName] = navigationOrigin;
        }

        public NavigatedTelemetryEvent(NavigationType navigationType, NavigationOrigin navigationOrigin, int sourcesCount, bool isGlobbing)
            : this(navigationType, navigationOrigin)
        {
            base[SourcesCountPropertyName] = sourcesCount;
            base[IsGlobbingPropertyName] = isGlobbing;
        }
    }
}
