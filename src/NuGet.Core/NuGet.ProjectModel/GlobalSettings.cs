// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.ProjectModel
{
    public class GlobalSettings
    {
        public const string GlobalFileName = "global.json";

        public IList<string> ProjectPaths { get; private set; }
        public string PackagesPath { get; private set; }
        public string FilePath { get; private set; }

        public string RootPath
        {
            get { return Path.GetDirectoryName(FilePath); }
        }

        public static bool TryGetGlobalSettings(string path, out GlobalSettings globalSettings)
        {
            globalSettings = null;

            string globalJsonPath = null;

            if (Path.GetFileName(path) == GlobalFileName)
            {
                globalJsonPath = path;
                path = Path.GetDirectoryName(path);
            }
            else if (!HasGlobalFile(path))
            {
                return false;
            }
            else
            {
                globalJsonPath = Path.Combine(path, GlobalFileName);
            }

            globalSettings = new GlobalSettings();

            try
            {
                var json = File.ReadAllText(globalJsonPath);

                JObject settings = JObject.Parse(json);

                var projects = settings["projects"];
                var dependencies = settings["dependencies"] as JObject;

                globalSettings.ProjectPaths = projects == null ? new string[] { } : projects.ValueAsArray<string>();
                globalSettings.PackagesPath = settings.Value<string>("packages");
                globalSettings.FilePath = globalJsonPath;
            }
            catch (Exception ex)
            {
                throw FileFormatException.Create(ex, globalJsonPath);
            }

            return true;
        }

        public static bool HasGlobalFile(string path)
        {
            var projectPath = Path.Combine(path, GlobalFileName);

            return File.Exists(projectPath);
        }
    }
}
