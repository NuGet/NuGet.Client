// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.VisualStudio.Setup.Configuration;

namespace NuGet.CommandLine
{
    /// <summary>
    /// Extended type to include Toolset data from the classic installation story and the appropriate values from
    /// ISetupInstance in the new (with MSBuild v15.1) installation story. Allows all discovered installations
    /// to be contained in the same collection.
    /// </summary>
    public class MsBuildToolset: IComparable
    {
        private readonly DateTime _installDate = DateTime.MinValue;
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
            _installDate = ConvertFILETIMEToDateTime(sxsToolset.GetInstallDate());
        }

        /// <summary>
        /// This constructor is for testing purposes only
        /// </summary>
        public MsBuildToolset(string version, string path, DateTime installDate)
        {
            Version = version;
            Path = path;
            _installDate = installDate;
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

        public int CompareTo(object obj)
        {
            if (Object.ReferenceEquals(obj, null))
            {
                return 1;
            }

            var rhs = obj as MsBuildToolset;
            if (obj == null)
            {
                throw new ArgumentException("Comparison object not of correct type");
            }

            // Compare versions
            var comparison = this.ParsedVersion.Major.CompareTo(rhs.ParsedVersion.Major);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = this.ParsedVersion.Minor.CompareTo(rhs.ParsedVersion.Minor);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = this.ParsedVersion.Build.CompareTo(rhs.ParsedVersion.Build);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = this.ParsedVersion.Revision.CompareTo(rhs.ParsedVersion.Revision);
            if (comparison != 0)
            {
                return comparison;
            }

            // Versions equal; compare by install date/time
            return this._installDate > rhs._installDate ? 1 : -1;
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

            string msBuildRoot = System.IO.Path.Combine(vsDir, "MSBuild");
            if (!Directory.Exists(msBuildRoot))
            {
                return null;
            }

            // Enumerate all versions of MSBuild present, take the highest
            string msBuildDirectory = string.Empty;
            var highestVersionRoot = Directory.EnumerateDirectories(msBuildRoot).OrderByDescending(dir =>
            {
                var dirName = new DirectoryInfo(dir).Name;
                float dirValue;
                if (float.TryParse(dirName, out dirValue))
                {
                    return dirValue;
                }

                return 0F;
            })
            .FirstOrDefault(dir =>
            {
                msBuildDirectory = System.IO.Path.Combine(dir, "bin");
                return File.Exists(System.IO.Path.Combine(msBuildDirectory, "msbuild.exe"));
            });

            return msBuildDirectory;
        }

        private static string GetMsBuildVersionFromMsBuildDir(string msBuildDir)
        {
            if (string.IsNullOrEmpty(msBuildDir))
            {
                return null;
            }

            var msBuildPath = System.IO.Path.Combine(msBuildDir, "msbuild.exe");
            if (!File.Exists(msBuildPath))
            {
                return null;
            }

            return FileVersionInfo.GetVersionInfo(msBuildPath)?.FileVersion;
        }
    }
}