using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml;

namespace NuGet.Tests.Foundation.Utility.Data
{
    // <summary>
    /// Provides static methods for creating XmlReader/XmlDocuments
    ///  - which safely instantiates local types only.
    /// </summary>
    /// <remarks>
    /// No Public XML FxCop documentation exists at the time this utility class was written
    /// See FxCop Rules: 
    ///     - CA3053 : Microsoft.Security.Xml an XmlReader instance...
    ///       This usage is potentially unsafe as untrusted external resources may be resolved
    ///       during read operations. Provide a XmlReaderSettings instance and set the XmlResolver
    ///       property to null or an instance of XmlSecureResolver
    /// </remarks>
    public static class XmlUtility
    {
        /// <summary>
        /// Create an XmlReader which will not instantiate external types
        /// </summary>
        [SuppressMessage("Microsoft.Security.Xml", "CA3053:UseXmlSecureResolver", Justification = "Doh FxCop!! CreateNullResolvingXmlReaderSettings exactly does that!!")]
        public static XmlReader CreateXmlReader(FileStream fileStream)
        {
            return XmlReader.Create(fileStream, XmlUtility.CreateNullResolvingXmlReaderSettings());
        }

        /// <summary>
        /// Create an XmlReader which will not instantiate external types
        /// </summary>
        [SuppressMessage("Microsoft.Security.Xml", "CA3053:UseXmlSecureResolver", Justification = "Doh FxCop!! CreateNullResolvingXmlReaderSettings exactly does that!!")]
        public static XmlReader CreateXmlReader(Stream stream)
        {
            return XmlReader.Create(stream, XmlUtility.CreateNullResolvingXmlReaderSettings());
        }

        /// <summary>
        /// Create an XmlReader which will not instantiate external types
        /// </summary>
        [SuppressMessage("Microsoft.Security.Xml", "CA3053:UseXmlSecureResolver", Justification = "Doh FxCop!! CreateNullResolvingXmlReaderSettings exactly does that!!")]
        public static XmlReader CreateXmlReader(StringReader stringReader)
        {
            return XmlReader.Create(stringReader, XmlUtility.CreateNullResolvingXmlReaderSettings());
        }

        /// <summary>
        /// Create an XmlReader which will not instantiate external types
        /// </summary>
        [SuppressMessage("Microsoft.Security.Xml", "CA3053:UseXmlSecureResolver", Justification = "Doh FxCop!! CreateNullResolvingXmlReaderSettings exactly does that!!")]
        public static XmlReader CreateXmlReader(MemoryStream memoryStream)
        {
            return XmlReader.Create(memoryStream, XmlUtility.CreateNullResolvingXmlReaderSettings());
        }

        /// <summary>
        /// Create an XmlReader which will not instantiate external types
        /// </summary>
        [SuppressMessage("Microsoft.Security.Xml", "CA3053:UseXmlSecureResolver", Justification = "Doh FxCop!! CreateNullResolvingXmlReaderSettings exactly does that!!")]
        public static XmlReader CreateXmlReader(string inputUri)
        {
            return XmlReader.Create(inputUri, XmlUtility.CreateNullResolvingXmlReaderSettings());
        }

        /// <summary>
        /// Create an XmlReader which will not instantiate external types
        /// </summary>
        [SuppressMessage("Microsoft.Security.Xml", "CA3053:UseXmlSecureResolver", Justification = "Doh FxCop!! CreateNullResolvingXmlReaderSettings exactly does that!!")]
        public static XmlReader CreateSafeXmlReader(StringReader stringReader)
        {
            return XmlReader.Create(stringReader, XmlUtility.CreateNullResolvingXmlReaderSettings());
        }

        /// <summary>
        /// Create an XmlDocument which will not instantiate external types
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1059")]
        [SuppressMessage("Microsoft.Security.Xml", "CA3053")]
        public static XmlDocument CreateXmlDocument()
        {
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.XmlResolver = null;
            return xmlDocument;
        }

        private static XmlReaderSettings CreateNullResolvingXmlReaderSettings()
        {
            XmlReaderSettings xmlReaderSettings = new XmlReaderSettings();
            xmlReaderSettings.XmlResolver = null;
            return xmlReaderSettings;
        }
    }
}
