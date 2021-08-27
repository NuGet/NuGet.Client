// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.PackageManagement;

namespace NuGet.VisualStudio
{
    public class VSActionsTelemetryEvent : ActionsTelemetryEvent
    {
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
           bool packageNamespaceEnabled,
           int packageNamespaceSourcesCount,
           int packageNamespaceAllEntryCounts,
           int addedPackagesWithPackageNamespaceCount,
           int updatedPackageWithPackageNamespaceCount) :
            base(operationId, projectIds, operationType, startTime, status, packageCount, endTime, duration)
        {
            base[nameof(Source)] = source;
            base[nameof(PackageNamespaceEnabled)] = packageNamespaceEnabled;
            base[nameof(PackageNamespaceSourcesCount)] = packageNamespaceSourcesCount;
            base[nameof(PackageNamespaceAllEntryCounts)] = packageNamespaceAllEntryCounts;
            base[nameof(PackageNamespacAddedPackagesCount)] = addedPackagesWithPackageNamespaceCount;
            base[nameof(PackageNamespaceUpdatedPackagesCount)] = updatedPackageWithPackageNamespaceCount;
        }

        public OperationSource Source => (OperationSource)base[nameof(Source)];
        public bool PackageNamespaceEnabled => (bool)base[nameof(PackageNamespaceEnabled)];
        public int PackageNamespaceSourcesCount => (int)base[nameof(PackageNamespaceSourcesCount)];
        public int PackageNamespaceAllEntryCounts => (int)base[nameof(PackageNamespaceAllEntryCounts)];
        public int PackageNamespacAddedPackagesCount => (int)base[nameof(PackageNamespacAddedPackagesCount)];
        public int PackageNamespaceUpdatedPackagesCount => (int)base[nameof(PackageNamespaceUpdatedPackagesCount)];
    }
}
