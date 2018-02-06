﻿using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace NuGetClient.Test.Integration.Util
{
    public static class UAPSDKVersionHelper
    {
        private const string Windows10SDKRegKey = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows Kits\Installed Roots";
        private const string Windows10SDKValue = "KitsRoot10";
        private const string UAPPlatformsPath = @"Platforms\UAP";

        public static string GetEarliestUAPVersion()
        {
            return UAPSDKVersionHelper.GetUAPPlatformVersions().First();
        }

        public static string GetLatestUAPVersion()
        {
            return UAPSDKVersionHelper.GetUAPPlatformVersions().Last();
        }

        public static string[] GetUAPPlatformVersions()
        {
            string windows10SdkPath = (string)Registry.GetValue(Windows10SDKRegKey, Windows10SDKValue, null);
            string platformsFolder = Path.Combine(windows10SdkPath, UAPPlatformsPath);
            return Directory.GetDirectories(platformsFolder).Select(path => Path.GetFileName(path)).OrderBy(s => s).ToArray();
        }
    }
}
