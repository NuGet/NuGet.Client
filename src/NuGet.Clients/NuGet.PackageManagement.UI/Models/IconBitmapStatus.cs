// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.UI
{
    public enum IconBitmapStatus
    {
        None = 0,
        NeedToFetch,
        Fetching,
        MemoryCachedIcon,
        DownloadedIcon,
        EmbeddedIcon,
        DefaultIcon,
        DefaultIconDueToDecodingError,
        DefaultIconDueToNullStream,
        DefaultIconDueToNoPackageReader,
        DefaultIconDueToNetworkFailures,
        DefaultIconDueToWebExceptionBadNetwork,
        DefaultIconDueToWebExceptionOther,
        DefaultIconDueToRelativeUri
    }

    public static class IconBitmapStatusUtility
    {
        public static bool GetIsDefaultIcon(IconBitmapStatus bitmapStatus)
        {
            return (bitmapStatus == IconBitmapStatus.DefaultIcon ||
                bitmapStatus == IconBitmapStatus.DefaultIconDueToDecodingError ||
                bitmapStatus == IconBitmapStatus.DefaultIconDueToNullStream ||
                bitmapStatus == IconBitmapStatus.DefaultIconDueToNoPackageReader ||
                bitmapStatus == IconBitmapStatus.DefaultIconDueToNetworkFailures ||
                bitmapStatus == IconBitmapStatus.DefaultIconDueToWebExceptionBadNetwork ||
                bitmapStatus == IconBitmapStatus.DefaultIconDueToWebExceptionOther ||
                bitmapStatus == IconBitmapStatus.DefaultIconDueToRelativeUri);
        }

        public static bool GetIsCompleted(IconBitmapStatus bitmapStatus)
        {
            return !(bitmapStatus == IconBitmapStatus.None ||
                bitmapStatus == IconBitmapStatus.NeedToFetch ||
                bitmapStatus == IconBitmapStatus.Fetching);
        }
    }
}
