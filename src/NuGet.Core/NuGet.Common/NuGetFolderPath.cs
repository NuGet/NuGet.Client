// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Common
{
    internal enum NuGetFolderPath
    {
        MachineWideSettingsBaseDirectory,
        MachineWideConfigDirectory,
        UserSettingsDirectory,
        HttpCacheDirectory,
        NuGetHome,
        DefaultMsBuildPath
    }
}