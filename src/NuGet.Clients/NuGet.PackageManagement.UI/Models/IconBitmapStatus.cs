// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.UI
{
    public enum IconBitmapStatus
    {
        None = 0,
        NeedToFetch,
        FetchQueued,
        ShowingFromCache,
        ShowingFromUrl,
        ShowingFromEmbeddedIcon,
        ShowingDefault
    }
}
