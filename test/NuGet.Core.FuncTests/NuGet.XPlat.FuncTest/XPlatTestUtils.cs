using System.IO;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.XPlat.FuncTest
{
    public static class XPlatTestUtils
    {
        /// <summary>
        /// Add a dependency to project.json.
        /// </summary>
        public static void AddDependency(JObject json, string id, string version)
        {
            var deps = (JObject)json["dependencies"];

            deps.Add(new JProperty(id, version));
        }

        /// <summary>
        /// Basic netcoreapp1.0 config
        /// </summary>
        public static JObject BasicConfigNetCoreApp
        {
            get
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["netcoreapp1.0"] = new JObject();

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                return json;
            }
        }

        /// <summary>
        /// Write a json file to disk.
        /// </summary>
        public static void WriteJson(JObject json, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            using (var fs = File.Open(outputPath, FileMode.CreateNew))
            using (var sw = new StreamWriter(fs))
            using (var writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.Indented;

                var serializer = new JsonSerializer();
                serializer.Serialize(writer, json);
            }
        }

        /// <summary>
        /// Copies test sources configuration to a test folder
        /// </summary>
        public static string CopyFuncTestConfig(string destinationFolder)
        {
            var sourceConfigFolder = NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);
            var sourceConfigFile = Path.Combine(sourceConfigFolder, "NuGet.Core.FuncTests.Config");
            var destConfigFile = Path.Combine(destinationFolder, "NuGet.Config");
            File.Copy(sourceConfigFile, destConfigFile);
            return destConfigFile;
        }

        public static string ReadApiKey(string feedName)
        {
            string fullPath = NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);
            using (Stream configStream = File.OpenRead(Path.Combine(fullPath, "NuGet.Protocol.FuncTest.config")))
            {
                var doc = XDocument.Load(XmlReader.Create(configStream));
                var element = doc.Root.Element(feedName);

                return element?.Element("ApiKey")?.Value;
            }
        }
    }
}
