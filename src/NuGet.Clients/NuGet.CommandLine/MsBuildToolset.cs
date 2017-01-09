﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.VisualStudio.Setup.Configuration;
using SysPath = System.IO.Path;

namespace NuGet.CommandLine
{
    /// <summary>
    /// Extended type to include Toolset data from the classic installation story and the appropriate values from
    /// ISetupInstance in the new (with MSBuild v15.1) installation story. Allows all discovered installations
    /// to be contained in the same collection.
    /// </summary>
    public class MsBuildToolset: IComparable<MsBuildToolset>
    {
        private Version _parsedToolsVersion;

        /// <summary>
        /// This constructor services pre-v15.1 and non-Windows toolsets
        /// </summary>
        public MsBuildToolset(string version, string path)
        {
            Version = version;
            Path = path;
        }

        /// <summary>
        /// This constructor services v15.1+ toolsets
        /// </summary>
        internal MsBuildToolset(ISetupInstance sxsToolset)
        {
            Path = GetMsBuildDirFromVsDir(sxsToolset.GetInstallationPath());
            Version = GetMsBuildVersionFromMsBuildDir(Path);
            InstallDate = ConvertFILETIMEToDateTime(sxsToolset.GetInstallDate());
        }

        /// <summary>
        /// This constructor is for testing purposes only
        /// </summary>
        public MsBuildToolset(string version, string path, DateTime installDate)
        {
            Version = version;
            Path = path;
            InstallDate = installDate;
        }

        public Version ParsedVersion
        {
            get
            {
                if (_parsedToolsVersion == null)
                {
                    if (!System.Version.TryParse(Version, out _parsedToolsVersion))
                    {
                        _parsedToolsVersion = new Version(0, 0);
                    }
                }

                return _parsedToolsVersion;
            }
        }

        public string Version { get; private set; }

        public string Path { get; private set; }

        public DateTime InstallDate { get; private set; } = DateTime.MinValue;

        public int CompareTo(MsBuildToolset rhs)
        {
            if (Object.ReferenceEquals(rhs, null))
            {
                return 1;
            }

            // Compare versions
            var comparison = this.ParsedVersion.CompareTo(rhs.ParsedVersion);
            if (comparison != 0)
            {
                return comparison;
            }

            // Versions equal; compare by install date/time
            return this.InstallDate.CompareTo(rhs.InstallDate);
        }

        private static DateTime ConvertFILETIMEToDateTime(FILETIME time)
        {
            long highBits = time.dwHighDateTime;
            highBits = highBits << 32;
            return DateTime.FromFileTimeUtc(highBits | (long)(uint)time.dwLowDateTime);
        }

        public static string GetMsBuildDirFromVsDir(string vsDir)
        {
            if (string.IsNullOrEmpty(vsDir))
            {
                return null;
            }

            string msBuildRoot = SysPath.Combine(vsDir, "MSBuild");
            if (!Directory.Exists(msBuildRoot))
            {
                return null;
            }

            // Enumerate all versions of MSBuild present, take the highest
            string msBuildDirectory = string.Empty;
            var highestVersionRoot = Directory.EnumerateDirectories(msBuildRoot)
                .OrderByDescending(ToFloatValue)
                .FirstOrDefault(dir =>
                {
                    msBuildDirectory = SysPath.Combine(dir, "bin");
                    return File.Exists(SysPath.Combine(msBuildDirectory, "msbuild.exe"));
                });

            return msBuildDirectory;
        }

        private static float ToFloatValue(string directoryName)
        {
            var dirName = new DirectoryInfo(directoryName).Name;
            float dirValue;
            if (float.TryParse(dirName, out dirValue))
            {
                return dirValue;
            }

            return 0F;
        }

        private static string GetMsBuildVersionFromMsBuildDir(string msBuildDir)
        {
            if (string.IsNullOrEmpty(msBuildDir))
            {
                return null;
            }

            var msBuildPath = SysPath.Combine(msBuildDir, "msbuild.exe");
            if (!File.Exists(msBuildPath))
            {
                return null;
            }

            return FileVersionInfo.GetVersionInfo(msBuildPath)?.FileVersion;
        }
    }
}