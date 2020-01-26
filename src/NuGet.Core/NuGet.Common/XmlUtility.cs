using System;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Common
{
    public static class XmlUtility
    {
        /// <summary>
        /// Creates a new System.Xml.Linq.XDocument from an System.Xml.XmlReader.
        /// </summary>
        /// <param name="filePath">Path for the file containing the XML data.</param>
        /// <returns>An <see cref="System.Xml.Linq.XDocument"/> contains the contents of the specified Xml file.</returns>
        public static XDocument Load(string filePath)
        {
            //This overloaded method of XmlReader.Create creates an instance of
            //XmlReaderSettings with default values that are safe
            using (var reader = XmlReader.Create(filePath))
            {
                return XDocument.Load(reader);
            }
        }
    }
}
