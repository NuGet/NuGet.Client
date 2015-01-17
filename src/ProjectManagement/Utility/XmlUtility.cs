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

        public static XDocument GetOrCreateDocument(XName rootName, string root, string path)
        {
            if (File.Exists(Path.Combine(root, path)))
            {
                try
                {
                    return GetDocument(root, path);
                }
                catch (FileNotFoundException)
                {
                    return CreateDocument(rootName, root, path);
                }
            }
            return CreateDocument(rootName, root, path);
        }

        public static XDocument CreateDocument(XName rootName, string root, string path)
        {
            var fullPath = Path.Combine(root, path);
            XDocument document = new XDocument(new XElement(rootName));
            // Add it to the file system
            FileSystemUtility.AddFile(root, path, document.Save);
            return document;
        }

        public static XDocument GetDocument(string root, string path)
        {
            var fullPath = Path.Combine(root, path);
            using (Stream configStream = File.OpenRead(fullPath))
            {
                return XmlUtility.LoadSafe(configStream, LoadOptions.PreserveWhitespace);
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
