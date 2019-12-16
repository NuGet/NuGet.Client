// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using Microsoft.Build.Framework;

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
            var projectFile = new FileInfo(context.ProjectFilePath);

            if (!TryGetPathOfFileAbove(GlobalJsonFileName, projectFile.Directory, out var globalJsonPath))
            {
                return null;
            }

            // Read the contents of global.json
            var globalJsonContents = File.ReadAllText(globalJsonPath);

            // Look ahead in the contents to see if there is an msbuild-sdks section.  Deserializing the file requires us to load
            // additional assemblies which can be a waste since global.json is usually ~100 bytes of text.
            if (globalJsonContents.IndexOf(MSBuildSdksPropertyName, StringComparison.Ordinal) == -1)
            {
                return null;
            }

            try
            {
                return ParseMSBuildSdksFromGlobalJson(globalJsonPath);
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
        /// <param name="fileName">The name of the file to search for.</param>
        /// <param name="startingDirectory">An optional <see cref="DirectoryInfo" /> representing the directory to start the search in.</param>
        /// <param name="result">Receives the <see cref="FileInfo" /> of the file if found, otherwise <code>null</code>.</param>
        /// <returns><code>true</code> if a file was found in the directory or any parent directory, otherwise <code>false</code>.</returns>
        public static bool TryGetPathOfFileAbove(string fileName, DirectoryInfo startingDirectory, out string result)
        {
            result = null;

            if (startingDirectory == null || !startingDirectory.Exists)
            {
                return false;
            }

            var lookInDirectory = startingDirectory;

            do
            {
                var possibleFile = new FileInfo(Path.Combine(lookInDirectory.FullName, fileName));

                if (possibleFile.Exists)
                {
                    result = possibleFile.FullName;

                    return true;
                }

                lookInDirectory = lookInDirectory.Parent;
            }
            while (lookInDirectory != null);

            return false;
        }

        /// <summary>
        /// Parses a global.json and returns the MSBuild SDK versions.
        /// </summary>
        /// <remarks>
        /// NoInlining is enabled ensure that System.Runtime.Serialization.Json.dll isn't loaded unless the method is called.
        /// </remarks>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static Dictionary<string, string> ParseMSBuildSdksFromGlobalJson(string path)
        {
            Dictionary<string, string> sdks = null;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max))
            {
                while (reader.Read())
                {
                    if (reader.LocalName.Equals(MSBuildSdksPropertyName) && reader.Depth == 1)
                    {
                        while (reader.Read())
                        {
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                string name = reader.LocalName.Trim();

                                if (!string.IsNullOrWhiteSpace(name) && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                {
                                    if (sdks == null)
                                    {
                                        sdks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                    }

                                    var value = reader.Value.Trim();

                                    if (!string.IsNullOrWhiteSpace(value))
                                    {
                                        sdks[name] = value;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return sdks;
        }
    }
}
