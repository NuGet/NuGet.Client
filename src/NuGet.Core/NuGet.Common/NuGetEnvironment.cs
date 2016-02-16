// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Common
{
    public static class NuGetEnvironment
    {
        public static string GetFolderPath(NuGetFolderPath folder)
        {
            switch (folder)
            {
                case NuGetFolderPath.MachineWideSettingsBaseDirectory:
                    return GetFolderPath(SpecialFolder.CommonApplicationData);

                case NuGetFolderPath.MachineWideConfigDirectory:
                    return Path.Combine(
                        GetFolderPath(NuGetFolderPath.MachineWideSettingsBaseDirectory),
                        "nuget",
                        "Config");

                case NuGetFolderPath.UserSettingsDirectory:
                    return Path.Combine(
                        GetFolderPath(SpecialFolder.ApplicationData),
                        "NuGet");

                case NuGetFolderPath.NuGetHome:
                    return Path.Combine(
                        GetFolderPath(SpecialFolder.UserProfile),
                        ".nuget");

                case NuGetFolderPath.HttpCacheDirectory:
                    return Path.Combine(
                        GetFolderPath(SpecialFolder.LocalApplicationData),
                        "NuGet",
                        "v3-cache");

                case NuGetFolderPath.DefaultMsBuildPath:
                    var programFilesPath = GetFolderPath(SpecialFolder.ProgramFilesX86);
                    if (string.IsNullOrEmpty(programFilesPath))
                    {
                        // On 32-bit Windows
                        programFilesPath = GetFolderPath(SpecialFolder.ProgramFiles);
                    }

                    return Path.Combine(programFilesPath, "MSBuild", "14.0", "Bin", "MSBuild.exe");

                case NuGetFolderPath.Temp:
                    return Path.Combine(Path.GetTempPath(), "NuGetScratch");

                default:
                    return null;
            }
        }

#if DNXCORE50
        private static string GetFolderPath(SpecialFolder folder)
        {
            switch (folder)
            {
                case SpecialFolder.ProgramFilesX86:
                    return Environment.GetEnvironmentVariable("PROGRAMFILES(X86)");

                case SpecialFolder.ProgramFiles:
                    return Environment.GetEnvironmentVariable("PROGRAMFILES");

                case SpecialFolder.UserProfile:
                    return GetHome();

                case SpecialFolder.CommonApplicationData:
                    if (RuntimeEnvironmentHelper.IsWindows)
                    {
                        string programData = Environment.GetEnvironmentVariable("PROGRAMDATA");

                        if (!string.IsNullOrEmpty(programData))
                        {
                            return programData;
                        }

                        return Environment.GetEnvironmentVariable("ALLUSERSPROFILE");
                    }
                    else if (RuntimeEnvironmentHelper.IsMacOSX)
                    {
                        return @"/Library/Application Support";
                    }
                    else
                    {
                        return @"/etc/opt";
                    }

                case SpecialFolder.ApplicationData:
                    if (RuntimeEnvironmentHelper.IsWindows)
                    {
                        return Environment.GetEnvironmentVariable("APPDATA");
                    }
                    else
                    {
                        return Path.Combine(GetHome(), ".nuget");
                    }

                case SpecialFolder.LocalApplicationData:
                    if (RuntimeEnvironmentHelper.IsWindows)
                    {
                        return Environment.GetEnvironmentVariable("LOCALAPPDATA");
                    }
                    else
                    {
                        string xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                        if (!string.IsNullOrEmpty(xdgDataHome))
                        {
                            return xdgDataHome;
                        }

                        return Path.Combine(GetHome(), ".local", "share");
                    }

                default:
                    return null;
            }
        }
#else
        private static string GetFolderPath(SpecialFolder folder)
        {
            // Convert the private enum to the .NET Framework enum
            Environment.SpecialFolder converted;
            switch (folder)
            {
                case SpecialFolder.ProgramFilesX86:
                    converted = Environment.SpecialFolder.ProgramFilesX86;
                    break;

                case SpecialFolder.ProgramFiles:
                    converted = Environment.SpecialFolder.ProgramFiles;
                    break;

                case SpecialFolder.UserProfile:
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                    // On Kudu this might return null
                    if (!string.IsNullOrEmpty(userProfile))
                    {
                        return userProfile;
                    }

                    return GetHome();

                case SpecialFolder.CommonApplicationData:
                    converted = Environment.SpecialFolder.CommonApplicationData;
                    break;

                case SpecialFolder.ApplicationData:
                    converted = Environment.SpecialFolder.ApplicationData;
                    break;

                case SpecialFolder.LocalApplicationData:
                    converted = Environment.SpecialFolder.LocalApplicationData;
                    break;

                default:
                    return null;
            }

            return Environment.GetFolderPath(converted);
        }
#endif

        private static string GetHome()
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
                if (!string.IsNullOrEmpty(userProfile))
                {
                    return userProfile;
                }
                else
                {
                    return Environment.GetEnvironmentVariable("HOMEDRIVE") + Environment.GetEnvironmentVariable("HOMEPATH");
                }
            }
            else
            {
                return Environment.GetEnvironmentVariable("HOME");
            }
        }


        /// <summary>
        /// Since <see cref="Environment.SpecialFolder"/> is not available on .NET Core, we have to
        /// make our own and re-implement the functionality. On .NET Framework, we can use the
        /// built-in functionality.
        /// </summary>
        private enum SpecialFolder
        {
            ProgramFilesX86,
            ProgramFiles,
            UserProfile,
            CommonApplicationData,
            ApplicationData,
            LocalApplicationData
        }
    }
}