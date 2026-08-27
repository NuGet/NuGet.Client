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
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Shared;
using NuGet.Versioning;
using JsonException = System.Text.Json.JsonException;

namespace NuGet.ProjectModel
{
    public static class PackagesLockFileFormat
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
        private static readonly JsonDocumentOptions DocumentOptions = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };
        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = true
        };

        public static PackagesLockFile Parse(string lockFileContent, string path)
        {
            return Parse(lockFileContent, NullLogger.Instance, path);
        }

        public static PackagesLockFile Parse(string lockFileContent, ILogger log, string path)
        {
            using (var reader = new StringReader(lockFileContent))
            {
                return Read(reader, log, path);
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
                PackagesLockFile lockFile;
                if (NuGetFeatureFlags.UseSystemTextJsonDeserializationFeatureSwitch
                    || NuGetFeatureFlags.IsSystemTextJsonDeserializationEnabledByEnvironment())
                {
                    lockFile = ReadLockFileWithSystemTextJson(stream);
                }
                else
                {
                    using var textReader = new StreamReader(stream);
                    lockFile = ReadLockFile(textReader);
                }

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

        public static PackagesLockFile Read(TextReader reader, ILogger log, string path)
        {
            try
            {
                var lockFile = ReadLockFile(reader);
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

        private static PackagesLockFile ReadLockFile(TextReader reader)
        {
            return ReadLockFile(reader, environmentVariableReader: null);
        }

        internal static PackagesLockFile ReadLockFile(
            TextReader reader,
            IEnvironmentVariableReader environmentVariableReader)
        {
            if (NuGetFeatureFlags.UseSystemTextJsonDeserializationFeatureSwitch)
            {
                return ReadLockFileWithSystemTextJson(reader);
            }

            if (NuGetFeatureFlags.IsSystemTextJsonDeserializationEnabledByEnvironment(environmentVariableReader))
            {
                return ReadLockFileWithSystemTextJson(reader);
            }

            return ReadLockFile(JsonUtility.LoadJson(reader));
        }

        internal static PackagesLockFile ReadLockFileWithSystemTextJson(TextReader reader)
        {
            using JsonDocument document = JsonDocument.Parse(reader.ReadToEnd(), DocumentOptions);
            return ReadLockFile(document.RootElement);
        }

        internal static PackagesLockFile ReadLockFileWithSystemTextJson(Stream stream)
        {
            using (stream)
            {
                if (stream.RequiresTextReader())
                {
                    using var reader = new StreamReader(stream);
                    return ReadLockFileWithSystemTextJson(reader);
                }

                using JsonDocument document = JsonDocument.Parse(stream, DocumentOptions);
                return ReadLockFile(document.RootElement);
            }
        }

        private static PackagesLockFile ReadLockFile(JObject cursor)
        {
            int version = JsonUtility.ReadInt(cursor, VersionProperty, defaultValue: int.MinValue);
            IList<PackagesLockFileTarget> targets;

            if (version >= AliasedVersion)
            {
                // V3 format: read from root level (alias/rid keys with framework and dependencies inside)
                targets = new List<PackagesLockFileTarget>();
                foreach (var property in cursor.Properties())
                {
                    if (property.Name != VersionProperty)
                    {
                        var target = ReadTargetV3(property.Name, property.Value);
                        if (target != null)
                        {
                            targets.Add(target);
                        }
                    }
                }
            }
            else
            {
                // V1 and V2 format: read from dependencies property
                targets = JsonUtility.ReadObject(cursor[DependenciesProperty] as JObject, ReadDependencyV2);
            }

            var lockFile = new PackagesLockFile()
            {
                Version = version,
                Targets = targets,
            };

            return lockFile;
        }

        private static PackagesLockFile ReadLockFile(JsonElement cursor)
        {
            int version = ReadInt(cursor, VersionProperty, defaultValue: int.MinValue);
            IList<PackagesLockFileTarget> targets;

            if (version >= AliasedVersion)
            {
                // V3 format: read from root level (alias/rid keys with framework and dependencies inside)
                targets = new List<PackagesLockFileTarget>();

                foreach (JsonProperty property in cursor.GetUniqueProperties())
                {
                    if (property.Name != VersionProperty)
                    {
                        var target = ReadTargetV3(property.Name, property.Value);
                        if (target != null)
                        {
                            targets.Add(target);
                        }
                    }
                }
            }
            else
            {
                // V1 and V2 format: read from dependencies property
                targets = cursor.TryGetProperty(DependenciesProperty, out JsonElement dependencies)
                    ? ReadObject(dependencies, ReadDependencyV2)
                    : new List<PackagesLockFileTarget>(0);
            }

            var lockFile = new PackagesLockFile()
            {
                Version = version,
                Targets = targets,
            };

            return lockFile;
        }

        private static int ReadInt(JsonElement json, string propertyName, int defaultValue)
        {
            if (!json.TryGetProperty(propertyName, out JsonElement value))
            {
                return defaultValue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String
                && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }

            return defaultValue;
        }

        private static IList<T> ReadObject<T>(JsonElement json, Func<string, JsonElement, T> readItem)
        {
            if (json.ValueKind != JsonValueKind.Object)
            {
                return new List<T>(0);
            }

            List<JsonProperty> properties = json.GetUniqueProperties();
            var items = new List<T>(properties.Count);
            foreach (JsonProperty property in properties)
            {
                items.Add(readItem(property.Name, property.Value));
            }

            return items;
        }

        private static string ReadString(JsonElement json, string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out JsonElement value))
            {
                return null;
            }

            return ReadValueAsString(value);
        }

        private static string ReadValueAsString(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.Null => null,
                _ => throw new JsonException()
            };
        }

        public static string Render(PackagesLockFile lockFile)
        {
            using (var writer = new StringWriter())
            {
                Write(writer, lockFile);
                return writer.ToString();
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
#if NET5_0_OR_GREATER
            using (var textWriter = new StreamWriter(stream))
#else
            using (var textWriter = new NoAllocNewLineStreamWriter(stream))
#endif
            {
                Write(textWriter, lockFile);
            }
        }

        public static void Write(TextWriter textWriter, PackagesLockFile lockFile)
        {
            Write(textWriter, lockFile, environmentVariableReader: null);
        }

        internal static void Write(
            TextWriter textWriter,
            PackagesLockFile lockFile,
            IEnvironmentVariableReader environmentVariableReader)
        {
            if (NuGetFeatureFlags.UseSystemTextJsonDeserializationFeatureSwitch
                || NuGetFeatureFlags.IsSystemTextJsonDeserializationEnabledByEnvironment(environmentVariableReader))
            {
                WriteWithSystemTextJson(textWriter, lockFile);
            }
            else
            {
                WriteWithNewtonsoftJson(textWriter, lockFile);
            }
        }

        private static void WriteWithSystemTextJson(TextWriter textWriter, PackagesLockFile lockFile)
        {
            using (var stream = new MemoryStream())
            {
                using (var jsonWriter = new Utf8JsonWriter(stream, WriterOptions))
                {
                    WriteLockFile(jsonWriter, lockFile);
                }

                string json = Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
                if (textWriter.NewLine != "\n")
                {
                    json = json.Replace("\n", textWriter.NewLine);
                }

                textWriter.Write(json);
            }
        }

        private static void WriteWithNewtonsoftJson(TextWriter textWriter, PackagesLockFile lockFile)
        {
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                JObject json = WriteLockFileWithNewtonsoftJson(lockFile);
#pragma warning disable IL2026, IL3050 // WriteTo without converters is safe. See https://github.com/JamesNK/Newtonsoft.Json/blob/13.0.4/Src/Newtonsoft.Json/Linq/JToken.cs
                json.WriteTo(jsonWriter, Array.Empty<Newtonsoft.Json.JsonConverter>());
#pragma warning restore IL2026, IL3050
            }
        }

        private static void WriteLockFile(Utf8JsonWriter writer, PackagesLockFile lockFile)
        {
            writer.WriteStartObject();
            writer.WriteNumber(VersionProperty, lockFile.Version);

            if (lockFile.Version >= AliasedVersion)
            {
                // V3 format: write targets at root level with framework and dependencies inside
                foreach (PackagesLockFileTarget target in lockFile.Targets)
                {
                    WriteTargetV3(writer, target);
                }
            }
            else
            {
                // V1 and V2 format: write targets under dependencies property
                writer.WritePropertyName(DependenciesProperty);
                WriteObject(writer, lockFile.Targets, WriteTarget);
            }

            writer.WriteEndObject();
        }

        private static JObject WriteLockFileWithNewtonsoftJson(PackagesLockFile lockFile)
        {
            var json = new JObject
            {
                [VersionProperty] = new JValue(lockFile.Version)
            };

            if (lockFile.Version >= AliasedVersion)
            {
                // V3 format: write targets at root level with framework and dependencies inside
                foreach (PackagesLockFileTarget target in lockFile.Targets)
                {
                    json.Add(WriteTargetV3WithNewtonsoftJson(target));
                }
            }
            else
            {
                // V1 and V2 format: write targets under dependencies property
                json[DependenciesProperty] = JsonUtility.WriteObject(lockFile.Targets, WriteTargetWithNewtonsoftJson);
            }

            return json;
        }

        private static PackagesLockFileTarget ReadDependencyV2(string property, JToken json)
        {
            var parts = property.Split(JsonUtility.PathSplitChars);

            var target = new PackagesLockFileTarget
            {
                TargetFramework = NuGetFramework.Parse(parts[0]),
                Dependencies = JsonUtility.ReadObject(json as JObject, ReadTargetDependency)
            };

            if (parts.Length == 2)
            {
                target.RuntimeIdentifier = parts[1];
            }

            return target;
        }

        private static PackagesLockFileTarget ReadTargetV3(string property, JToken json)
        {
            var jObject = json as JObject;
            if (jObject == null)
            {
                return null;
            }

            var frameworkString = JsonUtility.ReadProperty<string>(jObject, FrameworkProperty);
            if (string.IsNullOrEmpty(frameworkString))
            {
                return null;
            }

            var parts = property.Split(JsonUtility.PathSplitChars);

            var target = new PackagesLockFileTarget
            {
                TargetFramework = NuGetFramework.Parse(frameworkString),
                RuntimeIdentifier = parts.Length == 2 ? parts[1] : null,
                TargetAlias = parts[0],
                Dependencies = JsonUtility.ReadObject(jObject[DependenciesProperty] as JObject, ReadTargetDependency)
            };

            return target;
        }

        private static LockFileDependency ReadTargetDependency(string property, JToken json)
        {
            var dependency = new LockFileDependency
            {
                Id = property,
                Dependencies = JsonUtility.ReadObject(json[DependenciesProperty] as JObject, JsonUtility.ReadPackageDependency)
            };

            var jObject = json as JObject;

            var typeString = JsonUtility.ReadProperty<string>(jObject, TypeProperty);

            if (!string.IsNullOrEmpty(typeString)
                && Enum.TryParse<PackageDependencyType>(typeString, ignoreCase: true, result: out var installationType))
            {
                dependency.Type = installationType;
            }

            var resolvedString = JsonUtility.ReadProperty<string>(jObject, ResolvedProperty);

            if (!string.IsNullOrEmpty(resolvedString))
            {
                dependency.ResolvedVersion = NuGetVersion.Parse(resolvedString);
            }

            var requestedString = JsonUtility.ReadProperty<string>(jObject, RequestedProperty);

            if (!string.IsNullOrEmpty(requestedString))
            {
                dependency.RequestedVersion = VersionRange.Parse(requestedString);
            }

            dependency.ContentHash = JsonUtility.ReadProperty<string>(jObject, ContentHashProperty);

            return dependency;
        }

        private static PackagesLockFileTarget ReadDependencyV2(string property, JsonElement json)
        {
            var parts = property.Split(JsonUtility.PathSplitChars);

            var target = new PackagesLockFileTarget
            {
                TargetFramework = NuGetFramework.Parse(parts[0]),
                Dependencies = ReadObject(json, ReadTargetDependency)
            };

            if (parts.Length == 2)
            {
                target.RuntimeIdentifier = parts[1];
            }

            return target;
        }

        private static PackagesLockFileTarget ReadTargetV3(string property, JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string frameworkString = ReadString(json, FrameworkProperty);
            if (string.IsNullOrEmpty(frameworkString))
            {
                return null;
            }

            var parts = property.Split(JsonUtility.PathSplitChars);

            var target = new PackagesLockFileTarget
            {
                TargetFramework = NuGetFramework.Parse(frameworkString),
                RuntimeIdentifier = parts.Length == 2 ? parts[1] : null,
                TargetAlias = parts[0],
                Dependencies = json.TryGetProperty(DependenciesProperty, out JsonElement dependencies)
                    ? ReadObject(dependencies, ReadTargetDependency)
                    : new List<LockFileDependency>(0)
            };

            return target;
        }

        private static LockFileDependency ReadTargetDependency(string property, JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException();
            }

            var dependency = new LockFileDependency
            {
                Id = property,
                Dependencies = json.TryGetProperty(DependenciesProperty, out JsonElement dependencies)
                    ? ReadObject(dependencies, ReadPackageDependency)
                    : new List<PackageDependency>(0)
            };

            string typeString = ReadString(json, TypeProperty);

            if (!string.IsNullOrEmpty(typeString)
                && Enum.TryParse<PackageDependencyType>(typeString, ignoreCase: true, result: out var installationType))
            {
                dependency.Type = installationType;
            }

            string resolvedString = ReadString(json, ResolvedProperty);

            if (!string.IsNullOrEmpty(resolvedString))
            {
                dependency.ResolvedVersion = NuGetVersion.Parse(resolvedString);
            }

            string requestedString = ReadString(json, RequestedProperty);

            if (!string.IsNullOrEmpty(requestedString))
            {
                dependency.RequestedVersion = VersionRange.Parse(requestedString);
            }

            dependency.ContentHash = ReadString(json, ContentHashProperty);

            return dependency;
        }

        private static PackageDependency ReadPackageDependency(string property, JsonElement json)
        {
            string versionRange = ReadValueAsString(json);

            return new PackageDependency(
                property,
                versionRange == null ? null : VersionRange.Parse(versionRange));
        }

        private static void WriteTargetV3(Utf8JsonWriter writer, PackagesLockFileTarget target)
        {
            writer.WritePropertyName(target.Name);
            writer.WriteStartObject();
            writer.WriteString(FrameworkProperty, target.TargetFramework.ToString());
            writer.WritePropertyName(DependenciesProperty);
            WriteObject(writer, target.Dependencies, WriteTargetDependency);
            writer.WriteEndObject();
        }

        private static void WriteTarget(Utf8JsonWriter writer, PackagesLockFileTarget target)
        {
            writer.WritePropertyName(target.Name);
            WriteObject(writer, target.Dependencies, WriteTargetDependency);
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

                foreach (PackageDependency packageDependency in orderedDependencies)
                {
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
            Action<Utf8JsonWriter, T> writeItem)
        {
            writer.WriteStartObject();

            foreach (T item in items)
            {
                writeItem(writer, item);
            }

            writer.WriteEndObject();
        }

        private static JProperty WriteTargetV3WithNewtonsoftJson(PackagesLockFileTarget target)
        {
            var json = new JObject
            {
                [FrameworkProperty] = target.TargetFramework.ToString(),
                [DependenciesProperty] = JsonUtility.WriteObject(target.Dependencies, WriteTargetDependencyWithNewtonsoftJson)
            };

            return new JProperty(target.Name, json);
        }

        private static JProperty WriteTargetWithNewtonsoftJson(PackagesLockFileTarget target)
        {
            JObject json = JsonUtility.WriteObject(target.Dependencies, WriteTargetDependencyWithNewtonsoftJson);
            return new JProperty(target.Name, json);
        }

        private static JProperty WriteTargetDependencyWithNewtonsoftJson(LockFileDependency dependency)
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
                IOrderedEnumerable<PackageDependency> orderedDependencies = dependency.Dependencies.OrderBy(
                    dependency => dependency.Id,
                    StringComparer.Ordinal);

                json[DependenciesProperty] = JsonUtility.WriteObject(
                    orderedDependencies,
                    dependency.Type == PackageDependencyType.Project
                        ? JsonUtility.WritePackageDependency
                        : JsonUtility.WritePackageDependencyWithLegacyString);
            }

            return new JProperty(dependency.Id, json);
        }

    }
}
