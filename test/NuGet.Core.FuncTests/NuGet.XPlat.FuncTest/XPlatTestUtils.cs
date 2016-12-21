// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
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

        public static void WaitForDebugger()
        {
            Console.WriteLine("Waiting for debugger to attach.");
            Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");

            while (!Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(100);
            }
            Debugger.Break();
        }

        public static XDocument LoadSafe(string filePath)
        {
            var settings = CreateSafeSettings();
            using (var reader = XmlReader.Create(filePath, settings))
            {
                return XDocument.Load(reader);
            }
        }

        public static XmlReaderSettings CreateSafeSettings(bool ignoreWhiteSpace = false)
        {
            var safeSettings = new XmlReaderSettings
            {
#if !IS_CORECLR
                XmlResolver = null,
#endif
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = ignoreWhiteSpace
            };

            return safeSettings;
        }
    }
}