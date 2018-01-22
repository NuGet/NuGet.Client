// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    public class PackageExtractionTelemetryEvent : TelemetryEvent
    {
        public const string EventName = "PackageExtractionInformation";

        public PackageSaveMode PackageSaveMode => (PackageSaveMode)base[nameof(PackageSaveMode)];

        public NuGetOperationStatus Status => (NuGetOperationStatus)base[nameof(Status)];

        public ExtractionSource ExtractionSource => (ExtractionSource)base[nameof(ExtractionSource)];

        public string PackageId => (string)base[nameof(PackageId)];

        public PackageExtractionTelemetryEvent(
            PackageSaveMode packageSaveMode,
            NuGetOperationStatus status,
            ExtractionSource extractionSource,
            PackageIdentity packageId) :
            base(EventName, new Dictionary<string, object>
                {
                    { nameof(Status), status },
                    { nameof(ExtractionSource), extractionSource },
                    { nameof(PackageSaveMode), packageSaveMode }
                })
        {
            AddPiiData(nameof(PackageId), packageId.ToString());
        }
    }
}
