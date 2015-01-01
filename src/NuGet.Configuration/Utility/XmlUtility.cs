using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    internal static class XmlUtility
    {
        internal static string GetOptionalAttributeValue(XElement element, string localName, string namespaceName = null)
        {
            XAttribute attr;
            if (String.IsNullOrEmpty(namespaceName))
            {
                attr = element.Attribute(localName);
            }
            else
            {
                attr = element.Attribute(XName.Get(localName, namespaceName));
            }
            return attr != null ? attr.Value : null;
        }

        internal static XDocument GetOrCreateDocument(XName rootName, string fullPath)
        {
            if (File.Exists(fullPath))
            {
                try
                {
                    return GetDocument(fullPath);
                }
                catch (FileNotFoundException)
                {
                    return CreateDocument(rootName, fullPath);
                }
            }
            return CreateDocument(rootName, fullPath);
        }

        private static XDocument CreateDocument(XName rootName, string fullPath)
        {
            XDocument document = new XDocument(new XElement(rootName));
            // Add it to the file system
            FileSystemUtility.AddFile(fullPath, document.Save);
            return document;
        }

        private static XDocument GetDocument(string fullPath)
        {
            using (Stream configStream = File.OpenRead(fullPath))
            {
                return XmlUtility.LoadSafe(configStream, LoadOptions.PreserveWhitespace);
            }
        }

        private static XDocument LoadSafe(Stream input, LoadOptions options)
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
    }
}
