// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.Versioning;

namespace NuGet.PackageManagement.Telemetry
{
    public class SearchSelectionTelemetryEvent : TelemetryEvent
    {
        public SearchSelectionTelemetryEvent(
            Guid parentId,
            int itemIndex,
            string packageId,
            NuGetVersion packageVersion) : base("SearchSelection")
        {
            base["ParentId"] = parentId.ToString();
            base["ItemIndex"] = itemIndex;
            AddPiiData("PackageId", packageId.ToLowerInvariant());
            AddPiiData("PackageVersion", packageVersion.ToNormalizedString().ToLowerInvariant());
        }
    }
}
