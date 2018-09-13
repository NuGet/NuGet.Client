// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.Packaging
{
    public class NupkgMetadataFileFormat
    {
        public static readonly int Version = 1;

        private const string VersionProperty = "version";
        private const string HashProperty = "contentHash";

        private static readonly JsonLoadSettings DefaultLoadSettings = new JsonLoadSettings()
        {
            LineInfoHandling = LineInfoHandling.Ignore,
            CommentHandling = CommentHandling.Ignore
        };

        public static NupkgMetadataFile Read(string filePath)
        {
            return Read(filePath, NullLogger.Instance);
        }

        public static NupkgMetadataFile Read(string filePath, ILogger log)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return Read(stream, log, filePath);
            }
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
            try
            {
                var json = LoadJson(reader);
                var hashFile = ReadHashFile(json);
                return hashFile;
            }
            catch (Exception ex)
            {
                log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_LoadingHashFile,
                    path, ex.Message));

                throw;
            }
        }

        private static JObject LoadJson(TextReader reader)
        {
            using (var jsonReader = new JsonTextReader(reader))
            {
                while (jsonReader.TokenType != JsonToken.StartObject)
                {
                    if (!jsonReader.Read())
                    {
                        throw new InvalidDataException();
                    }
                }

                return JObject.Load(jsonReader, DefaultLoadSettings);
            }
        }

        private static NupkgMetadataFile ReadHashFile(JObject cursor)
        {
            var hashFile = new NupkgMetadataFile()
            {
                Version = ReadInt(cursor, VersionProperty, defaultValue: int.MinValue),
                ContentHash = ReadProperty<string>(cursor, HashProperty),
            };

            return hashFile;
        }

        internal static int ReadInt(JToken cursor, string property, int defaultValue)
        {
            var valueToken = cursor[property];
            if (valueToken == null)
            {
                return defaultValue;
            }
            return valueToken.Value<int>();
        }

        internal static TItem ReadProperty<TItem>(JObject jObject, string propertyName)
        {
            if (jObject != null)
            {
                JToken value;
                if (jObject.TryGetValue(propertyName, out value) && value != null)
                {
                    return value.Value<TItem>();
                }
            }

            return default(TItem);
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

                var json = WriteHashFile(hashFile);
                json.WriteTo(jsonWriter);
            }
        }

        private static JObject WriteHashFile(NupkgMetadataFile hashFile)
        {
            var json = new JObject
            {
                [VersionProperty] = new JValue(hashFile.Version),
                [HashProperty] = hashFile.ContentHash
            };

            return json;
        }

    }
}
