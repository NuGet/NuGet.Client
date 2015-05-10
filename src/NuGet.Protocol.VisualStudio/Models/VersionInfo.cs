// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    public class VersionInfo
    {
        public VersionInfo(NuGetVersion version, int? downloadCount)
        {
            Version = version;
            DownloadCount = downloadCount;
        }

        public NuGetVersion Version { get; private set; }

        public int? DownloadCount { get; private set; }
    }
}
