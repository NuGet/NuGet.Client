// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public static partial class PackagesLockFileFormat
    {
        public static readonly int Version = 1;
        internal static readonly int AliasedVersion = 3;

        // This allows us to maintain compatibility with older clients that don't understand the concept of central package versions.
        public static readonly int PackagesLockFileVersion = AliasedVersion;

        public static readonly string LockFileName = "packages.lock.json";

        private const string VersionProperty = "version";
        private const string ResolvedProperty = "resolved";
        private const string RequestedProperty = "requested";
        private const string ContentHashProperty = "contentHash";
        private const string DependenciesProperty = "dependencies";
        private const string TypeProperty = "type";
        private const string FrameworkProperty = "framework";
        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = true,
#if NET9_0_OR_GREATER
            // Match Newtonsoft.Json's platform-specific newline behavior.
            NewLine = Environment.NewLine
#endif
        };

        public static PackagesLockFile Parse(string lockFileContent, string path)
        {
            return Parse(lockFileContent, NullLogger.Instance, path);
        }

        public static PackagesLockFile Parse(string lockFileContent, ILogger log, string path)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(lockFileContent), writable: false))
            {
                return Read(stream, log, path);
            }
        }

        public static PackagesLockFile Read(string filePath)
        {
            return Read(filePath, NullLogger.Instance);
        }

        public static PackagesLockFile Read(string filePath, ILogger log)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return Read(stream, log, filePath);
            }
        }

        public static PackagesLockFile Read(Stream stream, ILogger log, string path)
        {
            try
            {
                PackagesLockFile lockFile = ReadLockFile(stream);
                lockFile.Path = path;
                return lockFile;
            }
            catch (Exception ex)
            {
                log.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.Log_ErrorReadingLockFile,
                    path, ex.Message));

                // Ran into parsing errors, mark it as unlocked and out-of-date
                return new PackagesLockFile
                {
                    Version = int.MinValue,
                    Path = path
                };
            }
        }

        internal static PackagesLockFile ReadLockFile(Stream stream)
        {
            using (stream)
            {
                var reader = new Utf8JsonStreamReader(stream);
                try
                {
                    return ReadLockFile(ref reader);
                }
                finally
                {
                    reader.Dispose();
                }
            }
        }

        public static string Render(PackagesLockFile lockFile)
        {
            using (var stream = new MemoryStream())
            {
                WriteToStream(stream, lockFile);

                return Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
            }
        }

        public static void Write(string filePath, PackagesLockFile lockFile)
        {
            // Create the directory if it does not exist
            var fileInfo = new FileInfo(filePath);
            fileInfo.Directory.Create();

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(stream, lockFile);
            }
        }

        public static void Write(Stream stream, PackagesLockFile lockFile)
        {
            using (stream)
            {
                WriteToStream(stream, lockFile);
            }
        }

        [Obsolete("Use Write(Stream, PackagesLockFile) instead.")]
        public static void Write(TextWriter textWriter, PackagesLockFile lockFile)
        {
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                var json = WriteLockFile(lockFile);
#pragma warning disable IL2026, IL3050 // WriteTo without converters is safe. See https://github.com/JamesNK/Newtonsoft.Json/blob/13.0.4/Src/Newtonsoft.Json/Linq/JToken.cs
                json.WriteTo(jsonWriter, Array.Empty<JsonConverter>());
#pragma warning restore IL2026, IL3050
            }
        }

        private static void WriteToStream(Stream stream, PackagesLockFile lockFile)
        {
            using (var jsonWriter = new Utf8JsonWriter(stream, WriterOptions))
            {
                WriteLockFile(jsonWriter, lockFile);
            }
        }

        private static JObject WriteLockFile(PackagesLockFile lockFile)
        {
            var json = new JObject
            {
                [VersionProperty] = new JValue(lockFile.Version)
            };

            if (lockFile.Version >= AliasedVersion)
            {
                // V3 format: write targets at root level with framework and dependencies inside
                foreach (var target in lockFile.Targets)
                {
                    var targetProperty = WriteTargetV3(target);
                    json.Add(targetProperty);
                }
            }
            else
            {
                // V1 and V2 format: write targets under dependencies property
                json[DependenciesProperty] = JsonUtility.WriteObject(lockFile.Targets, WriteTarget);
            }

            return json;
        }

        private static void WriteLockFile(Utf8JsonWriter writer, PackagesLockFile lockFile)
        {
            writer.WriteStartObject();
            writer.WriteNumber(VersionProperty, lockFile.Version);

            if (lockFile.Version >= AliasedVersion)
            {
                // V3 format: write targets at root level with framework and dependencies inside
                var targetNames = new HashSet<string>(StringComparer.Ordinal) { VersionProperty };
                foreach (PackagesLockFileTarget target in lockFile.Targets)
                {
                    ThrowIfDuplicate(targetNames, target.Name);
                    WriteTargetV3(writer, target);
                }
            }
            else
            {
                // V1 and V2 format: write targets under dependencies property
                writer.WritePropertyName(DependenciesProperty);
                WriteObject(writer, lockFile.Targets, target => target.Name, WriteTarget);
            }

            writer.WriteEndObject();
        }

        private static JProperty WriteTargetV3(PackagesLockFileTarget target)
        {
            var key = target.Name;

            var json = new JObject
            {
                [FrameworkProperty] = target.TargetFramework.ToString(),
                [DependenciesProperty] = JsonUtility.WriteObject(target.Dependencies, WriteTargetDependency)
            };

            return new JProperty(key, json);
        }

        private static JProperty WriteTarget(PackagesLockFileTarget target)
        {
            var json = JsonUtility.WriteObject(target.Dependencies, WriteTargetDependency);

            var key = target.Name;

            return new JProperty(key, json);
        }


        private static JProperty WriteTargetDependency(LockFileDependency dependency)
        {
            var json = new JObject
            {
                [TypeProperty] = dependency.Type.ToString()
            };

            if (dependency.RequestedVersion != null)
            {
                json[RequestedProperty] = dependency.RequestedVersion.ToNormalizedString();
            }

            if (dependency.ResolvedVersion != null)
            {
                json[ResolvedProperty] = dependency.ResolvedVersion.ToNormalizedString();
            }

            if (dependency.ContentHash != null)
            {
                json[ContentHashProperty] = dependency.ContentHash;
            }

            if (dependency.Dependencies?.Count > 0)
            {
                var ordered = dependency.Dependencies.OrderBy(dep => dep.Id, StringComparer.Ordinal);

                json[DependenciesProperty] = JsonUtility.WriteObject(ordered, dependency.Type == PackageDependencyType.Project ?
                    JsonUtility.WritePackageDependency : JsonUtility.WritePackageDependencyWithLegacyString);
            }

            return new JProperty(dependency.Id, json);
        }

        private static void WriteTargetV3(Utf8JsonWriter writer, PackagesLockFileTarget target)
        {
            writer.WritePropertyName(target.Name);
            writer.WriteStartObject();
            writer.WriteString(FrameworkProperty, target.TargetFramework.ToString());
            writer.WritePropertyName(DependenciesProperty);
            WriteObject(writer, target.Dependencies, dependency => dependency.Id, WriteTargetDependency);
            writer.WriteEndObject();
        }

        private static void WriteTarget(Utf8JsonWriter writer, PackagesLockFileTarget target)
        {
            writer.WritePropertyName(target.Name);
            WriteObject(writer, target.Dependencies, dependency => dependency.Id, WriteTargetDependency);
        }

        private static void WriteTargetDependency(Utf8JsonWriter writer, LockFileDependency dependency)
        {
            writer.WritePropertyName(dependency.Id);
            writer.WriteStartObject();
            writer.WriteString(TypeProperty, dependency.Type.ToString());

            if (dependency.RequestedVersion != null)
            {
                writer.WriteString(RequestedProperty, dependency.RequestedVersion.ToNormalizedString());
            }

            if (dependency.ResolvedVersion != null)
            {
                writer.WriteString(ResolvedProperty, dependency.ResolvedVersion.ToNormalizedString());
            }

            if (dependency.ContentHash != null)
            {
                writer.WriteString(ContentHashProperty, dependency.ContentHash);
            }

            if (dependency.Dependencies?.Count > 0)
            {
                IEnumerable<PackageDependency> orderedDependencies = dependency.Dependencies.OrderBy(
                    dependency => dependency.Id,
                    StringComparer.Ordinal);

                writer.WritePropertyName(DependenciesProperty);
                writer.WriteStartObject();

                var dependencyIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (PackageDependency packageDependency in orderedDependencies)
                {
                    ThrowIfDuplicate(dependencyIds, packageDependency.Id);
                    writer.WritePropertyName(packageDependency.Id);
                    string versionRange = dependency.Type == PackageDependencyType.Project
                        ? packageDependency.VersionRange?.ToString()
                        : packageDependency.VersionRange?.ToNonSnapshotRange().ToLegacyShortString();

                    if (versionRange == null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        writer.WriteStringValue(versionRange);
                    }
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void WriteObject<T>(
            Utf8JsonWriter writer,
            IEnumerable<T> items,
            Func<T, string> getPropertyName,
            Action<Utf8JsonWriter, T> writeItem)
        {
            writer.WriteStartObject();

            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (T item in items)
            {
                ThrowIfDuplicate(propertyNames, getPropertyName(item));
                writeItem(writer, item);
            }

            writer.WriteEndObject();
        }

        private static void ThrowIfDuplicate(HashSet<string> propertyNames, string propertyName)
        {
            if (!propertyNames.Add(propertyName))
            {
                throw new ArgumentException();
            }
        }

    }
}
