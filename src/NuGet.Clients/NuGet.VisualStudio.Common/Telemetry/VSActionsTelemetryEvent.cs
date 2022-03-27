// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.PackageManagement;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.VisualStudio
{
    public class VSActionsTelemetryEvent : ActionsTelemetryEvent
    {
        public bool IsSolutionLevel { set => this["IsSolutionLevel"] = value; }
        public ItemFilter Tab { set => this["Tab"] = value; }
        public bool IsPackageToInstallTransitive { set => this["IsPackageToInstallTransitive"] = value; }

        public VSActionsTelemetryEvent(
           string operationId,
           string[] projectIds,
           NuGetOperationType operationType,
           OperationSource source,
           DateTimeOffset startTime,
           NuGetOperationStatus status,
           int packageCount,
           DateTimeOffset endTime,
           double duration,
           bool isPackageSourceMappingEnabled) :
            base(operationId, projectIds, operationType, startTime, status, packageCount, endTime, duration)
        {
            base[nameof(Source)] = source;
            base[PackageSourceMappingIsMappingEnabled] = isPackageSourceMappingEnabled;
        }

        public OperationSource Source => (OperationSource)base[nameof(Source)];
        internal const string PackageSourceMappingIsMappingEnabled = "PackageSourceMapping.IsMappingEnabled";
    }
}
