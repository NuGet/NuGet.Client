// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.VisualStudio
{
    public class VsNuGetFramework : IVsNuGetFramework
    {
        public VsNuGetFramework(
            string targetFrameworkIdentifier,
            string targetFrameworkVersion,
            string profile,
            string targetPlatformIdentifier,
            string targetPlatformVersion)
        {
            TargetFrameworkIdentifier = targetFrameworkIdentifier;
            TargetFrameworkVersion = targetFrameworkVersion;
            TargetFrameworkProfile = profile;
            TargetPlatformIdentifier = targetPlatformIdentifier;
            TargetPlatformVersion = targetPlatformVersion;
        }

        public string TargetFrameworkIdentifier { get; }

        public string TargetFrameworkVersion { get; }

        public string TargetFrameworkProfile { get; }

        public string TargetPlatformIdentifier { get; }

        public string TargetPlatformVersion { get; }
    }
}
