// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using ContractsItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class RestoreBannerClickedTelemetryEvent : TelemetryEvent
    {
        internal const string RestoreBannerClickedEventName = "RestoreBannerClicked";
        internal const string RestoreButtonActionName = "RestoreButtonAction";
        internal const string RestoreButtonOriginPropertyName = "ProjectLevel";

        public RestoreBannerClickedTelemetryEvent(RestoreButtonAction restoreButtonAction, RestoreButtonOrigin restoreButtonOrigin)
            : base(RestoreBannerClickedEventName)
        {
            base[RestoreButtonActionName] = restoreButtonAction;
            base[RestoreButtonOriginPropertyName] = restoreButtonOrigin;
        }
    }

    public enum RestoreButtonAction
    {
        MissingAssetsFile,
        MissingPackages
    }

    public enum RestoreButtonOrigin
    {
        SolutionView,
        ProjectView,
        PackageManagerConsole
    }
}
