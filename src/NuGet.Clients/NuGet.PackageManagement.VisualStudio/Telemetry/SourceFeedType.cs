// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.Telemetry
{
    internal enum SourceFeedType
    {
        Http,
        LocalAbsolute,
        LocalRelative,
        Unc,
        MultiFeed,
        Unknown,
    }
}
