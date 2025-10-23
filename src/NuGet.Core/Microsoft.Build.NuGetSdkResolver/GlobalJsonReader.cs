// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.Common;

namespace Microsoft.Build.NuGetSdkResolver
{
    /// <summary>
    /// Represents an implementation of <see cref="IGlobalJsonReader" /> that reads MSBuild project SDK related sections from a global.json.
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/core/tools/global-json?#msbuild-sdks" />
    /// </summary>
    internal sealed class GlobalJsonReader : IGlobalJsonReader
    {
        /// <summary>
        /// The default name of the file containing configuration information.
        /// </summary>
        public const string GlobalJsonFileName = "global.json";

        /// <summary>
        /// The name of the section in global.json that contains MSBuild project SDK versions.
        /// </summary>
        public const string MSBuildSdksPropertyName = "msbuild-sdks";

        /// <summary>
        /// Represents a thread-safe cache for files based on their full path and last write time.
        /// </summary>
        private static readonly ConcurrentDictionary<FileInfo, (DateTime LastWriteTime, Lazy<Dictionary<string, string>?> Lazy)> FileCache = new ConcurrentDictionary<FileInfo, (DateTime, Lazy<Dictionary<string, string>?>)>(FileSystemInfoFullNameEqualityComparer.Instance);


        private GlobalJsonReader()
        {
        }

        public static GlobalJsonReader Instance { get; } = new GlobalJsonReader();

        /// <summary>
        /// Occurs when a file is read.
        /// </summary>
        public event EventHandler<string>? FileRead;

        public Dictionary<string, string>? GetMSBuildSdkVersions(SdkResolverContext? context, out string? globalJsonFullPath, string fileName = GlobalJsonFileName)
        {
            globalJsonFullPath = null;

            // If the SolutionFilePath and ProjectFilePath are not set, an in-memory project is being evaluated and there's no way to know which directory to start looking for a global.json
            if (string.IsNullOrWhiteSpace(fileName) || context is null || !TryGetStartingPath(context, out string? startingPath))
            {
                return null;
            }

            FileInfo? globalJsonPath;

            try
            {
                DirectoryInfo? projectDirectory = Directory.GetParent(startingPath);

                if (projectDirectory is null || !TryGetPathOfFileAbove(fileName, projectDirectory, out globalJsonPath))
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                // Failed to determine path to global.json from path "{0}". {1}
                context.Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, Strings.FailedToFindPathToGlobalJson, startingPath, e.Message));

                return null;
            }

            // Add a new file to the cache if it doesn't exist.  If the file is already in the cache, read it again if the file has changed
            (DateTime _, Lazy<Dictionary<string, string>?> Lazy) cacheEntry = FileCache.AddOrUpdate(
                globalJsonPath,
                key => (key.LastWriteTime, new Lazy<Dictionary<string, string>?>(() => ParseMSBuildSdkVersions(key.FullName, context))),
                (key, item) =>
                {
                    DateTime lastWriteTime = key.LastWriteTime;

                    if (item.LastWriteTime < lastWriteTime)
                    {
                        return (lastWriteTime, new Lazy<Dictionary<string, string>?>(() => ParseMSBuildSdkVersions(key.FullName, context)));
                    }

                    return item;
                });

            Dictionary<string, string>? sdkVersions = cacheEntry.Lazy.Value;

            globalJsonFullPath = globalJsonPath.FullName;

            return sdkVersions;
        }

        internal static bool TryGetStartingPath(SdkResolverContext? context, [NotNullWhen(true)] out string? startingPath)
        {
            startingPath = null;

            if (context == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(context.SolutionFilePath))
            {
                startingPath = context.SolutionFilePath;

                return true;
            }

            if (!string.IsNullOrWhiteSpace(context.ProjectFilePath))
            {
                startingPath = context.ProjectFilePath;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Searches for a file in the specified starting directory and any of the parent directories.
        /// </summary>
        /// <param name="file">The name of the file to search for.</param>
        /// <param name="startingDirectory">The <see cref="DirectoryInfo" /> to look in first and then search the parent directories of.</param>
        /// <param name="fullPath">Receives a <see cref="FileInfo" /> of the file if one is found, otherwise <see langword="null" />.</param>
        /// <returns><see langword="true" /> if the specified file was found in the directory or one of its parents, otherwise <see langword="false" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetPathOfFileAbove(string file, DirectoryInfo? startingDirectory, [NotNullWhen(true)] out FileInfo? fullPath)
        {
            fullPath = null;

            if (string.IsNullOrWhiteSpace(file) || startingDirectory is null || !startingDirectory.Exists)
            {
                return false;
            }

            DirectoryInfo? currentDirectory = startingDirectory;

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
        /// Parses the <c>msbuild-sdks</c> section of the specified JSON string.
        /// </summary>
        /// <param name="json">The JSON to parse as a string.</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}" /> containing MSBuild project SDK versions if any were found, otherwise <see langword="null" />.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
#pragma warning disable IDE0051 // Remove unused private members
        private static Dictionary<string, string>? ParseMSBuildSdkVersionsFromJson(string json)
#pragma warning restore IDE0051 // Remove unused private members
        {
            using var reader = new JsonTextReader(new StringReader(json));

            // Read to the first {
            while (reader.Read() && reader.TokenType != JsonToken.StartObject)
            {
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                // Return null if no { was found
                return null;
            }

            // Read through each top-level property
            while (reader.Read())
            {
                // Look for the first "msbuild-sdks" section
                if (reader.TokenType != JsonToken.PropertyName
                    || reader.Value is not string objectName
                    || !string.Equals(objectName, MSBuildSdksPropertyName, StringComparison.Ordinal)
                    || !reader.Read()
                    || reader.TokenType != JsonToken.StartObject)
                {
                    // Skip any top-level entry that's not a property
                    reader.Skip();

                    continue;
                }

                Dictionary<string, string>? versionsByName = null;

                // Read each token in the "msbuild-sdks" section until the end
                while (reader.Read()
                    && reader.TokenType != JsonToken.EndObject)
                {
                    // Only read properties of type string
                    if (reader.TokenType != JsonToken.PropertyName || reader.Value is not string name || !reader.Read() || reader.TokenType != JsonToken.String || reader.Value is not string value)
                    {
                        // Skips anything under the "mbsuild-sdks" section that wasn't a property of type string
                        reader.Skip();

                        continue;
                    }

                    versionsByName ??= new Dictionary<string, string>(capacity: 4, StringComparer.OrdinalIgnoreCase);

                    versionsByName[name] = value;
                }

                // Stop reading the global.json once the entire "mbsuild-sdks" section is read
                return versionsByName;
            }

            // Return null if an "msbuild-sdks" section was not found
            return null;
        }

        internal static Dictionary<string, string>? ParseMSBuildSdkVersionsFromStream(Stream jsonStream)
        {
            // Use a pooled buffer for minimal allocations
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

            try
            {
                int bytesRead;
                int bufferOffset = 0;
                // Fill buffer with initial data
                bytesRead = jsonStream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    return null;
                }

                Dictionary<string, string>? result = null;

                Utf8JsonReader reader = new(
                    new ReadOnlySpan<byte>(buffer, 0, bytesRead),
                    new JsonReaderOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });

                while (true)
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("msbuild-sdks"u8))
                            {
                                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                                {
                                    return null;
                                }

                                while (reader.Read())
                                {
                                    if (reader.TokenType == JsonTokenType.EndObject)
                                    {
                                        return result;
                                    }

                                    if (reader.TokenType != JsonTokenType.PropertyName)
                                    {
                                        reader.Skip();

                                        continue;
                                    }

                                    var name = reader.GetString()!;

                                    if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                                    {
                                        continue;
                                    }

                                    string value = reader.GetString()!;

                                    result ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                                    result[name] = value;
                                }

                                return null;
                            }
                        }
                    }

                    if (reader.BytesConsumed == bytesRead)
                    {
                        bufferOffset += bytesRead;
                        if (bufferOffset == buffer.Length)
                        {
                            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                            Buffer.BlockCopy(buffer, 0, newBuffer, 0, buffer.Length);
                            ArrayPool<byte>.Shared.Return(buffer);
                            buffer = newBuffer;
                        }
                        bytesRead = jsonStream.Read(buffer, bufferOffset, buffer.Length - bufferOffset);
                        if (bytesRead == 0)
                        {
                            return null;
                        }

                        reader = new Utf8JsonReader(new ReadOnlySpan<byte>(buffer, 0, bufferOffset + bytesRead), isFinalBlock: false, reader.CurrentState);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

        }

        /// <summary>
        /// Fires the <see cref="FileRead" /> event for the specified file.
        /// </summary>
        /// <param name="filePath">The full path to file that was read.</param>
        private void OnFileRead(string filePath)
        {
            EventHandler<string>? fileReadEventHandler = FileRead;

            fileReadEventHandler?.Invoke(this, filePath);
        }

        /// <summary>
        /// Parses the <c>msbuild-sdks</c> section of the specified file.
        /// </summary>
        /// <param name="globalJsonPath"></param>
        /// <param name="sdkResolverContext">The current <see cref="SdkResolverContext" /> to use.</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}" /> containing MSBuild project SDK versions if any were found, otherwise <see langword="null" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Dictionary<string, string>? ParseMSBuildSdkVersions(string globalJsonPath, SdkResolverContext sdkResolverContext)
        {
            if (NuGetEventSource.IsEnabled) TraceEvents.GlobalJsonReadStart(globalJsonPath, sdkResolverContext);

            Stream? fileStream = default;

            try
            {
                try
                {
                    fileStream = File.OpenRead(globalJsonPath);
                }
                catch (Exception e)
                {
                    // Failed to read file "{0}". {1}
                    sdkResolverContext.Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, Strings.FailedToReadGlobalJson, globalJsonPath, e.Message));

                    return null;
                }

                OnFileRead(globalJsonPath);

                try
                {
                    return ParseMSBuildSdkVersionsFromStream(fileStream);
                }
                catch (Exception e)
                {
                    // Failed to parse "{0}". {1}
                    sdkResolverContext.Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, Strings.FailedToParseGlobalJson, globalJsonPath, e.Message));

                    return null;
                }
            }
            finally
            {
                if (NuGetEventSource.IsEnabled) TraceEvents.GlobalJsonReadStop(globalJsonPath, sdkResolverContext);

                fileStream?.Dispose();
            }
        }

        private static class TraceEvents
        {
            private const string EventNameGlobalJsonRead = "SdkResolver/GlobalJsonRead";

            public static void GlobalJsonReadStart(string globalJsonPath, SdkResolverContext sdkResolverContext)
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.SdkResolver | NuGetEventSource.Keywords.Performance,
                    Opcode = EventOpcode.Start
                };

                NuGetEventSource.Instance.Write(EventNameGlobalJsonRead, eventOptions, new GlobalJsonReadEventData(globalJsonPath, sdkResolverContext.ProjectFilePath, sdkResolverContext.SolutionFilePath));
            }

            public static void GlobalJsonReadStop(string globalJsonPath, SdkResolverContext sdkResolverContext)
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.SdkResolver | NuGetEventSource.Keywords.Performance,
                    Opcode = EventOpcode.Stop
                };

                NuGetEventSource.Instance.Write(EventNameGlobalJsonRead, eventOptions, new GlobalJsonReadEventData(globalJsonPath, sdkResolverContext.ProjectFilePath, sdkResolverContext.SolutionFilePath));
            }

            [EventData]
            private record struct GlobalJsonReadEventData(string Path, string ProjectFullPath, string SolutionFullPath);
        }
    }
}
