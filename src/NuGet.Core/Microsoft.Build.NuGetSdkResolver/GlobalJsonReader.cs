// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;

namespace Microsoft.Build.NuGetSdkResolver
{
    /// <summary>
    /// Reads MSBuild related sections from a global.json.
    /// </summary>
    internal static class GlobalJsonReader
    {
        public const string GlobalJsonFileName = "global.json";

        private static readonly Regex MsBuildSdksSectionRegex = new Regex("\\\"msbuild-sdks\\\"\\s*:\\s*\\{(?<Packages>[^}]+)\\}", RegexOptions.None, TimeSpan.FromSeconds(1));

        private static readonly Regex MsBuildSdksContentsRegex = new Regex("\\\"(?<Id>[^\\\"]+)\\\"\\s*:\\s*\\\"(?<Version>[^\\\"]+)\\\"", RegexOptions.None, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Walks up the directory tree to find the first global.json and reads the msbuild-sdks section.
        /// </summary>
        /// <returns>A <see cref="Dictionary{String,String}"/> of MSBuild SDK versions from a global.json if found, otherwise <code>null</code>.</returns>
        public static Dictionary<string, string> GetMSBuildSdkVersions(SdkResolverContext context)
        {
            var projectDirectory = Directory.GetParent(context.ProjectFilePath);

            if (projectDirectory == null
                || !projectDirectory.Exists
                || !TryGetPathOfFileAbove(GlobalJsonFileName, projectDirectory.FullName, out var globalJsonPath))
            {
                return null;
            }
            
            var match = MsBuildSdksSectionRegex.Match(File.ReadAllText(globalJsonPath));

            if (!match.Success || !match.Groups["Packages"].Success)
            {
                return null;
            }

            try
            {
                var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var matches = MsBuildSdksContentsRegex.Matches(match.Groups["Packages"].Value);
                for (int i = 0; i < matches.Count; i++)
                {
                    if (matches[i].Success && matches[i].Groups["Id"].Success && matches[i].Groups["Version"].Success)
                    {
                        packages[matches[i].Groups["Id"].Value] = matches[i].Groups["Version"].Value;
                    }
                }

                return packages;
            }
            catch (Exception e)
            {
                // Failed to parse "{0}". {1}
                var message = string.Format(CultureInfo.CurrentCulture, Strings.FailedToParseGlobalJson, globalJsonPath, e.Message);
                context.Logger.LogMessage(message);
                return null;
            }
        }

        /// <summary>
        /// Searches for a file based on the specified starting directory.
        /// </summary>
        /// <param name="file">The file to search for.</param>
        /// <param name="startingDirectory">An optional directory to start the search in.  The default location is the directory
        /// of the file containing the property function.</param>
        /// <returns>The full path of the file if it is found, otherwise an empty string.</returns>
        private static string GetPathOfFileAbove(string file, string startingDirectory)
        {
            // Search for a directory that contains that file
            var directoryName = GetDirectoryNameOfFileAbove(startingDirectory, file);

            return string.IsNullOrEmpty(directoryName) ? string.Empty : NormalizePath(Path.Combine(directoryName, file));
        }

        private static bool TryGetPathOfFileAbove(string file, string startingDirectory, out string fullPath)
        {
            fullPath = GetPathOfFileAbove(file, startingDirectory);

            return fullPath != string.Empty;
        }

        /// <summary>
        /// Locate a file in either the directory specified or a location in the
        /// directory structure above that directory.
        /// </summary>
        private static string GetDirectoryNameOfFileAbove(string startingDirectory, string fileName)
        {
            // Canonicalize our starting location
            var lookInDirectory = Path.GetFullPath(startingDirectory);

            do
            {
                // Construct the path that we will use to test against
                var possibleFileDirectory = Path.Combine(lookInDirectory, fileName);

                // If we successfully locate the file in the directory that we're
                // looking in, simply return that location. Otherwise we'll
                // keep moving up the tree.
                if (File.Exists(possibleFileDirectory))
                {
                    // We've found the file, return the directory we found it in
                    return lookInDirectory;
                }
                else
                {
                    // GetDirectoryName will return null when we reach the root
                    // terminating our search
                    lookInDirectory = Path.GetDirectoryName(lookInDirectory);
                }
            }
            while (lookInDirectory != null);

            // When we didn't find the location, then return an empty string
            return string.Empty;
        }

        private static string NormalizePath(string path)
        {
            return FixFilePath(Path.GetFullPath(path));

        }
        private static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/');
        }
    }
}
