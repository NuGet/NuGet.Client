// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.Setup.Configuration;

namespace NuGet.CommandLine
{
    /// <summary>
    /// "Extended" type to include Toolset from the classic installation story and the appropriate values from
    /// ISetupInstance in the new (with MSBuild v15.1) installation story. Allows all discovered installations
    /// to be contained in the same collection.
    /// </summary>
    public class MsBuildToolsetEx: IComparable
    {
        private readonly Toolset _toolset;
        private readonly DateTime _installDateTime = DateTime.MinValue;

        public MsBuildToolsetEx(Toolset toolset)
        {
            _toolset = toolset;

            Version parsedToolsVersion;
            if (!Version.TryParse(_toolset?.ToolsVersion, out parsedToolsVersion))
            {
                parsedToolsVersion = new Version(0, 0);
            }
            ParsedToolsVersion = parsedToolsVersion;

            ToolsPath = GetMSBuildPathFromVsPath(_toolset?.ToolsPath);
            if (string.IsNullOrEmpty(ToolsPath))
            {
                ToolsPath = _toolset?.ToolsPath; // This fallback will service pre-v15.1 toolsets
            }
        }

        public MsBuildToolsetEx(Toolset toolset, DateTime installDateTime): this(toolset)
        {
            _installDateTime = installDateTime;
        }

        internal MsBuildToolsetEx(ISetupInstance sxsToolset): this(
            new Toolset(toolsVersion: sxsToolset.GetInstallationVersion(),
                        toolsPath: sxsToolset.GetInstallationPath(),
                        projectCollection: new ProjectCollection(),
                        msbuildOverrideTasksPath: string.Empty))
        {
            _installDateTime = ConvertFILETIMEToDateTime(sxsToolset.GetInstallDate());
        }

        public string ToolsVersion
        {
            get
            {
                return _toolset?.ToolsVersion;
            }
        }

        public Version ParsedToolsVersion { get; private set; }

        private static Version SafeParseVersion(string version)
        {
            Version result;

            if (Version.TryParse(version, out result))
            {
                return result;
            }
            else
            {
                return new Version(0, 0);
            }
        }

        public string ToolsPath { get; private set; }

        public static IEnumerable<MsBuildToolsetEx> AsMsToolsetExCollection(IEnumerable<Toolset> toolsets)
        {
            return toolsets.Select(t => new MsBuildToolsetEx(t));
        }

        public int CompareTo(object obj)
        {
            if (Object.ReferenceEquals(obj, null))
            {
                return 1;
            }

            var rhs = obj as MsBuildToolsetEx;
            if (obj == null)
            {
                throw new ArgumentException("Comparison object not of correct type");
            }

            // Compare versions
            if (this.ParsedToolsVersion.Major > rhs.ParsedToolsVersion.Major)
            {
                return 1;
            }
            if (this.ParsedToolsVersion.Major < rhs.ParsedToolsVersion.Major)
            {
                return -1;
            }

            // Major versions equal; compare minor versions
            if (this.ParsedToolsVersion.Minor > rhs.ParsedToolsVersion.Minor)
            {
                return 1;
            }
            if (this.ParsedToolsVersion.Minor < rhs.ParsedToolsVersion.Minor)
            {
                return -1;
            }

            // Versions equal; compare by install date/time
            return this._installDateTime > rhs._installDateTime ? 1 : -1;
        }

        private static DateTime ConvertFILETIMEToDateTime(FILETIME time)
        {
            long highBits = time.dwHighDateTime;
            highBits = highBits << 32;
            return DateTime.FromFileTimeUtc(highBits | (long)(uint)time.dwLowDateTime);
        }

        public static string GetMSBuildPathFromVsPath(string vsPath)
        {
            if (string.IsNullOrEmpty(vsPath))
            {
                return null;
            }

            string msBuildRoot = Path.Combine(vsPath, "MSBuild");
            if (!Directory.Exists(msBuildRoot))
            {
                return null;
            }

            // Enumerate all versions of MSBuild present, take the highest
            string msBuildPath = string.Empty;
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
                msBuildPath = Path.Combine(dir, "bin");
                return File.Exists(Path.Combine(msBuildPath, "msbuild.exe"));
            });

            return msBuildPath;
        }
    }
}