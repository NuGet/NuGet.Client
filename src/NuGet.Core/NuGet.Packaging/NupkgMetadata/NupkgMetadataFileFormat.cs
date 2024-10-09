// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using NuGet.Common;

namespace NuGet.Packaging
{
    public static class NupkgMetadataFileFormat
    {
        public static readonly int Version = 2;

        private const string VersionProperty = "version";
        private const string HashProperty = "contentHash";
        private const string SourceProperty = "source";

        private static readonly JsonSerializer JsonSerializer = JsonSerializer.Create(GetSerializerSettings());

        public static NupkgMetadataFile Read(string filePath)
        {
            return Read(filePath, NullLogger.Instance);
        }

        public static NupkgMetadataFile Read(string filePath, ILogger log)
        {
            return FileUtility.SafeRead(filePath, (stream, nupkgMetadataFilePath) => Read(stream, log, nupkgMetadataFilePath));
        }

        public static NupkgMetadataFile Read(Stream stream, ILogger log, string path)
        {
            using (var textReader = new StreamReader(stream))
            {
                return Read(textReader, log, path);
            }
        }

        public static NupkgMetadataFile Read(TextReader reader, ILogger log, string path)
        {
            List<string> errors = new List<string>();

            var serializerSettings = new JsonSerializerSettings
            {
                Error = delegate (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args)
                {
                    // Log the error
                    log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                        Strings.Error_LoadingHashFile,
                        path, args.ErrorContext.Error.Message));

                    // Add the error message to the list
                    errors.Add(args.ErrorContext.Error.Message);
                    args.ErrorContext.Handled = true;
                }
            };

            using (var jsonReader = new JsonTextReader(reader))
            {
                var serializer = JsonSerializer.Create(serializerSettings);
                var nupkgMetadata = serializer.Deserialize<NupkgMetadataFile>(jsonReader);

                if (errors.Count > 0)
                {
                    string errorMessage = $"Error: {string.Join("; ", errors)}";
                    throw new InvalidDataException(errorMessage);
                }
                else if (errors.Count == 0 && nupkgMetadata == null)
                {
                    var error = string.Format(CultureInfo.CurrentCulture,
                        Strings.Error_UnableToLoadNupkgMetadata,
                        path);
                    throw new InvalidDataException($"Error: {error}");
                }

                return nupkgMetadata;
            }
        }

        public static void Write(string filePath, NupkgMetadataFile hashFile)
        {
            // Create the directory if it does not exist
            var fileInfo = new FileInfo(filePath);
            fileInfo.Directory.Create();

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(stream, hashFile);
            }
        }

        public static void Write(Stream stream, NupkgMetadataFile hashFile)
        {
            using (var textWriter = new StreamWriter(stream))
            {
                Write(textWriter, hashFile);
            }
        }

        public static void Write(TextWriter textWriter, NupkgMetadataFile hashFile)
        {
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                JsonSerializer.Serialize(jsonWriter, hashFile);
            }
        }

        private static JsonSerializerSettings GetSerializerSettings()
        {
            var settings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented
            };
            settings.Converters.Add(NupkgMetadataConverter.Default);
            return settings;
        }

        private class NupkgMetadataConverter : JsonConverter
        {
            internal static NupkgMetadataConverter Default { get; } = new NupkgMetadataConverter();

            private static readonly Type TargetType = typeof(NupkgMetadataFile);
            public override bool CanConvert(Type objectType)
            {
                return objectType == TargetType;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                NupkgMetadataFile nupkgMetadataFile = existingValue as NupkgMetadataFile;
                if (nupkgMetadataFile == null)
                {
                    nupkgMetadataFile = new NupkgMetadataFile();
                }

                if (reader.TokenType != JsonToken.StartObject)
                {
                    throw new InvalidDataException();
                }

                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonToken.EndObject:
                            return nupkgMetadataFile;

                        case JsonToken.PropertyName:
                            switch ((string)reader.Value)
                            {
                                case VersionProperty:
                                    var intValue = reader.ReadAsInt32();
                                    if (intValue.HasValue)
                                    {
                                        nupkgMetadataFile.Version = intValue.Value;
                                    }
                                    break;

                                case HashProperty:
                                    nupkgMetadataFile.ContentHash = reader.ReadAsString();
                                    break;

                                case SourceProperty:
                                    nupkgMetadataFile.Source = reader.ReadAsString();
                                    break;
                            }
                            break;

                        default:
                            reader.Skip();
                            break;
                    }
                }

                throw new JsonReaderException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (!(value is NupkgMetadataFile nupkgMetadataFile))
                {
                    throw new ArgumentException(message: "value is not of type NupkgMetadataFile", paramName: nameof(value));
                }

                writer.WriteStartObject();

                writer.WritePropertyName(VersionProperty);
                writer.WriteValue(nupkgMetadataFile.Version);

                writer.WritePropertyName(HashProperty);
                writer.WriteValue(nupkgMetadataFile.ContentHash);

                writer.WritePropertyName(SourceProperty);
                writer.WriteValue(nupkgMetadataFile.Source);

                writer.WriteEndObject();
            }
        }
    }
}
