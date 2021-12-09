// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Newtonsoft.Json;

namespace Microsoft.Build.NuGetSdkResolver
{
    /// <summary>
    /// Reads MSBuild related sections from a global.json.
    /// </summary>
    internal static class GlobalJsonReader
    {
        public const string GlobalJsonFileName = "global.json";

        public const string MSBuildSdksPropertyName = "msbuild-sdks";

        /// <summary>
        /// Walks up the directory tree to find the first global.json and reads the msbuild-sdks section.
        /// </summary>
        /// <returns>A <see cref="Dictionary{String,String}"/> of MSBuild SDK versions from a global.json if found, otherwise <code>null</code>.</returns>
        public static Dictionary<string, string> GetMSBuildSdkVersions(SdkResolverContext context)
        {
            if (string.IsNullOrWhiteSpace(context?.ProjectFilePath))
            {
                // If the ProjectFilePath is not set, an in-memory project is being evaluated and there's no way to know which directory to start looking for a global.json
                return null;
            }

            DirectoryInfo projectDirectory = Directory.GetParent(context.ProjectFilePath);

            if (!TryGetPathOfFileAbove(GlobalJsonFileName, projectDirectory, out FileInfo globalJsonPath))
            {
                return null;
            }

            string contents = File.ReadAllText(globalJsonPath.FullName);

            // Look ahead in the contents to see if there is an msbuild-sdks section.  Deserializing the file requires us to load
            // Newtonsoft.Json which is 500 KB while a global.json is usually ~100 bytes of text.
            if (contents.IndexOf(MSBuildSdksPropertyName, StringComparison.Ordinal) == -1)
            {
                return null;
            }

            try
            {
                return Deserialize(contents);
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
        /// Deserializes a global.json and returns the MSBuild SDK versions
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Dictionary<string, string> Deserialize(string value)
        {
            return JsonConvert.DeserializeObject<GlobalJsonFile>(value).MSBuildSdks;
        }

        private sealed class GlobalJsonFile
        {
            [JsonProperty(MSBuildSdksPropertyName)]
            public Dictionary<string, string> MSBuildSdks { get; set; }
        }

        /// <summary>
        /// Searches for a file in the specified starting directory and any of the parent directories.
        /// </summary>
        /// <param name="file">The name of the file to search for.</param>
        /// <param name="startingDirectory">The <see cref="DirectoryInfo" /> to look in first and then search the parent directories of.</param>
        /// <param name="fullPath">Receives a <see cref="FileInfo" /> of the file if one is found, otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the specified file was found in the directory or one of its parents, otherwise <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetPathOfFileAbove(string file, DirectoryInfo startingDirectory, out FileInfo fullPath)
        {
            fullPath = null;

            if (string.IsNullOrWhiteSpace(file) || startingDirectory == null || !startingDirectory.Exists)
            {
                return false;
            }

            DirectoryInfo currentDirectory = startingDirectory;

            FileInfo candidatePath;

            do
            {
                candidatePath = new FileInfo(Path.Combine(currentDirectory.FullName, file));

                if (candidatePath.Exists)
                {
                    fullPath = candidatePath;

                    return true;
                }

                currentDirectory = currentDirectory.Parent;
            }
            while (currentDirectory != null);

            return false;
        }
    }
}
