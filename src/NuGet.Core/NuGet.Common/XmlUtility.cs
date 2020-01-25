using System;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Common
{
    public static class XmlUtility
    {
        //
        // Summary:
        //     Creates a new System.Xml.Linq.XDocument from an System.Xml.XmlReader.
        //
        // Parameters:
        //   filePath:
        //     The URI for the file containing the XML data.
        //
        // Returns:
        //     An System.Xml.Linq.XDocument that contains the contents of the specified System.Xml.XmlReader.
        public static XDocument Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            using (var reader = XmlReader.Create(filePath))
            {
                return XDocument.Load(reader);
            }
        }
    }
}
