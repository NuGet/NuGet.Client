// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
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
        /// Represents a thread-safe cache for files based on their full path and last write time.
        /// </summary>
        private static readonly ConcurrentDictionary<FileInfo, (DateTime LastWriteTime, Lazy<Dictionary<string, string>> Lazy)> FileCache = new ConcurrentDictionary<FileInfo, (DateTime, Lazy<Dictionary<string, string>>)>(FileSystemInfoFullNameEqualityComparer.Instance);

        /// <summary>
        /// Walks up the directory tree to find the first global.json and reads the msbuild-sdks section.
        /// </summary>
        /// <returns>A <see cref="Dictionary{String,String}"/> of MSBuild SDK versions from a global.json if found, otherwise <code>null</code>.</returns>
        public static Dictionary<string, string> GetMSBuildSdkVersions(SdkResolverContext context, out bool wasGlobalJsonRead, string fileName = GlobalJsonFileName)
        {
            bool wasRead = false;

            wasGlobalJsonRead = wasRead;

            if (string.IsNullOrWhiteSpace(context?.ProjectFilePath) || string.IsNullOrWhiteSpace(fileName))
            {
                // If the ProjectFilePath is not set, an in-memory project is being evaluated and there's no way to know which directory to start looking for a global.json
                return null;
            }

            DirectoryInfo projectDirectory = Directory.GetParent(context.ProjectFilePath);

            if (projectDirectory == null || !TryGetPathOfFileAbove(fileName, projectDirectory, out FileInfo globalJsonPath))
            {
                return null;
            }

            if (!globalJsonPath.Exists)
            {
                FileCache.TryRemove(globalJsonPath, out var _);

                return null;
            }

            // Add a new file to the cache if it doesn't exist.  If the file is already in the cache, read it again if the file has changed
            (DateTime LastWriteTime, Lazy<Dictionary<string, string>> Lazy) result = FileCache.AddOrUpdate(
                globalJsonPath,
                key => (key.LastWriteTime, new Lazy<Dictionary<string, string>>(() =>
                {
                    wasRead = true;

                    return ParseMSBuildSdkVersions(key, context);
                })),
                (key, item) =>
                {
                    DateTime lastWriteTime = key.LastWriteTime;

                    if (item.LastWriteTime < lastWriteTime)
                    {
                        return (lastWriteTime, new Lazy<Dictionary<string, string>>(() =>
                        {
                            wasRead = true;

                            return ParseMSBuildSdkVersions(key, context);
                        }));
                    }

                    return item;
                });

            Dictionary<string, string> sdkVersions = result.Lazy.Value;

            wasGlobalJsonRead = wasRead;

            return sdkVersions;
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

        /// <summary>
        /// Parses the <c>msbuild-sdks</c> section of the specified file.
        /// </summary>
        /// <param name="globalJsonPath"></param>
        /// <param name="sdkResolverContext">The current <see cref="SdkResolverContext" /> to use.</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}" /> containing MSBuild project SDK versions if any were found, otherwise <c>null</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Dictionary<string, string> ParseMSBuildSdkVersions(FileInfo globalJsonPath, SdkResolverContext sdkResolverContext)
        {
            // Load the file as a string and check if it has an msbuild-sdks section.  Parsing the contents requires Newtonsoft.Json.dll to be loaded which can be expensive
            string json = File.ReadAllText(globalJsonPath.FullName);

            // Look ahead in the contents to see if there is an msbuild-sdks section.  Deserializing the file requires us to load
            // Newtonsoft.Json which is 500 KB while a global.json is usually ~100 bytes of text.
            if (json.IndexOf(MSBuildSdksPropertyName, StringComparison.Ordinal) == -1)
            {
                return null;
            }

            try
            {
                return ParseMSBuildSdkVersionsFromJson(json);
            }
            catch (Exception e)
            {
                // Failed to parse "{0}". {1}
                sdkResolverContext.Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, Strings.FailedToParseGlobalJson, globalJsonPath, e.Message));

                return null;
            }
        }

        /// <summary>
        /// Parses the <c>msbuild-sdks</c> section of the specified JSON string.
        /// </summary>
        /// <param name="json">The JSON to parse as a string.</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}" /> containing MSBuild project SDK versions if any were found, otherwise <c>null</c>.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Dictionary<string, string> ParseMSBuildSdkVersionsFromJson(string json)
        {
            Dictionary<string, string> versionsByName = null;

            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                {
                    return null;
                }

                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.PropertyName && reader.Value is string objectName && string.Equals(objectName, MSBuildSdksPropertyName, StringComparison.Ordinal) && reader.Read() && reader.TokenType == JsonToken.StartObject)
                    {
                        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                        {
                            if (reader.TokenType == JsonToken.PropertyName && reader.Value is string name && reader.Read() && reader.TokenType == JsonToken.String && reader.Value is string value)
                            {
                                versionsByName ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                                versionsByName[name] = value;

                                continue;
                            }

                            reader.Skip();
                        }

                        return versionsByName;
                    }

                    // Skip any top-level entry that's not a property
                    reader.Skip();
                }
            }

            return versionsByName;
        }

        /// <summary>
        /// An <see cref="IEqualityComparer{T}" /> that compares <see cref="FileSystemInfo" /> objects by the value of the <see cref="FileSystemInfo.FullName" /> property.
        /// </summary>
        private class FileSystemInfoFullNameEqualityComparer : IEqualityComparer<FileSystemInfo>
        {
            /// <summary>
            /// Gets a static singleton for the <see cref="FileSystemInfoFullNameEqualityComparer" /> class.
            /// </summary>
            public static FileSystemInfoFullNameEqualityComparer Instance = new FileSystemInfoFullNameEqualityComparer();

            /// <summary>
            /// Initializes a new instance of the <see cref="FileSystemInfoFullNameEqualityComparer" /> class.
            /// </summary>
            private FileSystemInfoFullNameEqualityComparer()
            {
            }

            /// <summary>
            /// Determines whether the specified <see cref="FileSystemInfo" /> objects are equal by comparing their <see cref="FileSystemInfo.FullName" /> property.
            /// </summary>
            /// <param name="x">The first <see cref="FileSystemInfo" /> to compare.</param>
            /// <param name="y">The second <see cref="FileSystemInfo" /> to compare.</param>
            /// <returns><c>true</c> if the specified <see cref="FileSystemInfo" /> objects' <see cref="FileSystemInfo.FullName" /> property are equal, otherwise <c>false</c>.</returns>
            public bool Equals(FileSystemInfo x, FileSystemInfo y)
            {
                return string.Equals(x.FullName, y.FullName, StringComparison.Ordinal);
            }

            /// <summary>
            /// Returns a hash code for the specified <see cref="FileSystemInfo" /> object's <see cref="FileSystemInfo.FullName" /> property.
            /// </summary>
            /// <param name="obj">The <see cref="FileSystemInfo" /> for which a hash code is to be returned.</param>
            /// <returns>A hash code for the specified <see cref="FileSystemInfo" /> object's <see cref="FileSystemInfo.FullName" /> property..</returns>
            public int GetHashCode(FileSystemInfo obj)
            {
#if NETFRAMEWORK || NETSTANDARD
                return obj.FullName.GetHashCode();
#else
                return obj.FullName.GetHashCode(StringComparison.Ordinal);
#endif
            }
        }
    }
}
