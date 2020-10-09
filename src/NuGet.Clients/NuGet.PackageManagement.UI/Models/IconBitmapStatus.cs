// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.UI
{
    public enum IconBitmapStatus
    {
        None = 0,
        InProgress,
        NeedToFetch,
        MemoryCachedIcon,
        DownloadedIcon,
        EmbeddedIcon,
        DefaultIcon,
        DefaultIconDueToDecodingError,
        DefaultIconDueToNullStream,
        DefaultIconDueToNoPackageReader,
        DefaultIconDueToNetworkFailures,
        DefaultIconDueToWebExceptionBadNetwork,
        DefaultIconDueToWebExceptionOther
    }
}
