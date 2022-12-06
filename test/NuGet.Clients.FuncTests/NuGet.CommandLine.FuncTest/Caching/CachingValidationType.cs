// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.Test.Caching
{
    public enum CachingValidationType
    {
        CommandSucceeded,
        PackageInstalled,
        PackageInGlobalPackagesFolder,
        PackageInHttpCache,
        PackageFromHttpCacheUsed,
        PackageFromSourceUsed,
        PackageFromSourceNotUsed,
        PackageFromGlobalPackagesFolderUsed,
        DirectDownloadFilesDoNotExist,
        RestoreNoOp,
    }
}
