using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public class CacheFileFormat
    {
        private const string DGSpecHashProperty = "dgSpecHash";

        public static CacheFile Read(string filePath)
        {
            return Read(filePath, NullLogger.Instance);
        }

        public static CacheFile Read(string filePath, ILogger log)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return Read(stream, log, filePath);
            }
        }

        private static CacheFile Read(Stream stream, ILogger log, string path)
        {
            using (var textReader = new StreamReader(stream))
            {
                return Read(textReader, log, path);
            }
        }

        private static CacheFile Read(TextReader reader, ILogger log, string path)
        {
            try
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
                    var token = JToken.Load(jsonReader);
                    var cacheFile = ReadCacheFile(token as JObject);
                    return cacheFile;
                }
            }
            catch (Exception ex)
            {
                log.LogError(string.Format(CultureInfo.CurrentCulture,
                    "error reading a cache file {1} : {2}",
                    path, ex.Message));

                // Parsing error, the cache file is invalid. 
                return new CacheFile();
            }
        }

        public static void Write(string filePath, CacheFile lockFile)
        {
            // Create the directory if it does not exist
            var fileInfo = new FileInfo(filePath);
            fileInfo.Directory.Create();

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(stream, lockFile);
            }
        }

        public static void Write(Stream stream, CacheFile cacheFile)
        {
            using (var textWriter = new StreamWriter(stream))
            {
                Write(textWriter, cacheFile);
            }
        }

        private static void Write(TextWriter textWriter, CacheFile cacheFile)
        {
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;
                var json = WriteCacheFile(cacheFile);
                json.WriteTo(jsonWriter);
            }
        }

        private static CacheFile ReadCacheFile(JObject cursor)
        {
            var cacheFile = new CacheFile();
            cacheFile.DgSpecHash = ReadString(cursor[DGSpecHashProperty]);
            return cacheFile;
        }

        private static JToken WriteString(string item)
        {
            return item != null ? new JValue(item) : JValue.CreateNull();
        }

        private static JObject WriteCacheFile(CacheFile cacheFile)
        {
            var json = new JObject();
            json[DGSpecHashProperty] = WriteString(cacheFile.DgSpecHash);
            return json;
        }

        private static string ReadString(JToken json)
        {
            return json.Value<string>();
        }
    }
}
