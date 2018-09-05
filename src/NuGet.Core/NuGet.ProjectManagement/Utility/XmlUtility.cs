// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.ProjectManagement
{
    public static class XmlUtility
    {
        public static XDocument LoadSafe(string filePath)
        {
            var settings = CreateSafeSettings();
            using (var reader = XmlReader.Create(filePath, settings))
            {
                return XDocument.Load(reader);
            }
        }

        public static XDocument LoadSafe(Stream input)
        {
            var settings = CreateSafeSettings();
            var reader = XmlReader.Create(input, settings);
            return XDocument.Load(reader);
        }

        public static XDocument LoadSafe(Stream input, bool ignoreWhiteSpace)
        {
            var settings = CreateSafeSettings(ignoreWhiteSpace);
            var reader = XmlReader.Create(input, settings);
            return XDocument.Load(reader);
        }

        public static XDocument LoadSafe(Stream input, LoadOptions options)
        {
            var settings = CreateSafeSettings();
            var reader = XmlReader.Create(input, settings);
            return XDocument.Load(reader, options);
        }

        private static XmlReaderSettings CreateSafeSettings(bool ignoreWhiteSpace = false)
        {
            var safeSettings = new XmlReaderSettings
                {
                    XmlResolver = null,
                    DtdProcessing = DtdProcessing.Prohibit,
                    IgnoreWhitespace = ignoreWhiteSpace
                };

            return safeSettings;
        }

        public static XDocument GetOrCreateDocument(XName rootName, string path, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            if (File.Exists(Path.Combine(msBuildNuGetProjectSystem.ProjectFullPath, path)))
            {
                try
                {
                    return GetDocument(msBuildNuGetProjectSystem.ProjectFullPath, path);
                }
                catch (FileNotFoundException)
                {
                    return CreateDocument(rootName, path, msBuildNuGetProjectSystem);
                }
            }
            return CreateDocument(rootName, path, msBuildNuGetProjectSystem);
        }

        public static XDocument GetOrCreateDocument(XName rootName, string root, string path, INuGetProjectContext nuGetProjectContext)
        {
            if (File.Exists(Path.Combine(root, path)))
            {
                try
                {
                    return GetDocument(root, path);
                }
                catch (FileNotFoundException)
                {
                    return CreateDocument(rootName, root, path, nuGetProjectContext);
                }
            }
            return CreateDocument(rootName, root, path, nuGetProjectContext);
        }

        public static XDocument CreateDocument(XName rootName, string path, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            var document = new XDocument(new XElement(rootName));
            // Add it to the project system
            MSBuildNuGetProjectSystemUtility.AddFile(msBuildNuGetProjectSystem, path, document.Save);
            return document;
        }

        public static XDocument CreateDocument(XName rootName, string root, string path, INuGetProjectContext nuGetProjectContext)
        {
            var document = new XDocument(new XElement(rootName));
            // Add it to the file system
            FileSystemUtility.AddFile(root, path, document.Save, nuGetProjectContext);
            return document;
        }

        public static XDocument GetDocument(string root, string path)
        {
            var fullPath = Path.Combine(root, path);
            using (Stream configStream = File.OpenRead(fullPath))
            {
                return LoadSafe(configStream, LoadOptions.PreserveWhitespace);
            }
        }

        public static bool TryParseDocument(string content, out XDocument document)
        {
            document = null;
            try
            {
                document = XDocument.Parse(content);
                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }
    }
}
