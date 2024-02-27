// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NuGet.PackageManagement;

namespace NuGet.SolutionRestoreManager
{
    public interface IAuditCheckResultCachingService
    {
        AuditCheckResult? LastAuditCheckResult { get; set; }
        public bool HasAuditBeenCachedAtLeastOnce { get; }
    }
}
