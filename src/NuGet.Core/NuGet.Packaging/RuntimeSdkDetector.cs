// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    internal static class RuntimeSdkDetector
    {
        private static readonly Lazy<NuGetVersion> LazySdkVersion = new(GetSdkVersion);
        private static readonly Lazy<bool> LazyIs8OrGreater = new(GetIs8OrGreater);

        internal static bool Is8OrGreater => LazyIs8OrGreater.Value;

        private static bool GetIs8OrGreater()
        {
            NuGetVersion sdkVersion = LazySdkVersion.Value;

            return sdkVersion is not null && sdkVersion.Version >= new Version(8, 0, 0, 0);
        }

        private static NuGetVersion GetSdkVersion()
        {
            if (TryGetSdkVersion(out NuGetVersion version))
            {
                return version;
            }

            return null;
        }

        // Non-private for testing.
        internal static bool TryGetSdkVersion(out NuGetVersion version)
        {
            Assembly assembly = typeof(RuntimeSdkDetector).Assembly;
            string filePath = assembly.Location;

            return TryGetSdkVersion(filePath, out version);
        }

        // Non-private for testing.
        internal static bool TryGetSdkVersion(string filePath, out NuGetVersion version)
        {
            version = null;

            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            FileInfo file = new(filePath);
            string directoryName = file.Directory?.Name;

            return !string.IsNullOrEmpty(directoryName)
                && char.IsDigit(directoryName[0])
                && NuGetVersion.TryParse(directoryName, out version);
        }
    }
}
