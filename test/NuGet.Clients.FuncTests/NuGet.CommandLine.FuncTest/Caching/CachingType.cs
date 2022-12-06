// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.CommandLine.Test.Caching
{
    [Flags]
    public enum CachingType
    {
        Default = 0,
        NoCache = 1,
        DirectDownload = 2
    }
}
