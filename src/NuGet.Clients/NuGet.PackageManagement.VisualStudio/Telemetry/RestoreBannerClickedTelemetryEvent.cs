// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class RestoreBannerClickedTelemetryEvent : TelemetryEvent
    {
        internal const string RestoreBannerClickedEventName = "RestoreBannerClicked";
        internal const string RestoreButtonActionName = "RestoreButtonAction";

        public RestoreBannerClickedTelemetryEvent(RestoreButtonAction restoreButtonAction)
            : base(RestoreBannerClickedEventName)
        {
            base[RestoreButtonActionName] = restoreButtonAction;
        }
    }
}
