// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Shared
{
    internal static class XmlUtility
    {
        /// <summary>
        /// Creates a new System.Xml.Linq.XDocument from a file.
        /// </summary>
        /// <param name="filePath">A URI string that references the file to load into a new <see cref="System.Xml.Linq.XDocument"/></param>
        /// <returns>An <see cref="System.Xml.Linq.XDocument"/> that contains the contents of the specified file.</returns>
        internal static XDocument Load(string filePath)
        {
            return Load(filePath, LoadOptions.None);
        }

        internal static XDocument Load(string filePath,LoadOptions options)
        {
            using (var reader = XmlReader.Create(filePath, GetXmlReaderSettings(options)))
            {
                return XDocument.Load(reader,options);
            }
        }

        /// <summary>
        /// Creates a new System.Xml.Linq.XDocument from a stream.
        /// </summary>
        /// <param name="input">The stream that contains the XML data.</param>
        /// <returns>An <see cref="System.Xml.Linq.XDocument"/> that contains the contents of the specified stream.</returns>
        internal static XDocument Load(Stream input)
        {
            return Load(input, LoadOptions.None);
        }

        /// <summary>
        /// Creates a new System.Xml.Linq.XDocument from a stream.
        /// </summary>
        /// <param name="input">The stream that contains the XML data.</param>
        /// <param name="options">LoadOptions</param>
        /// <returns>An <see cref="System.Xml.Linq.XDocument"/> that contains the contents of the specified stream.</returns>
        internal static XDocument Load(Stream input, LoadOptions options)
        {
            using (var reader = XmlReader.Create(input, GetXmlReaderSettings(options)))
            {
                return XDocument.Load(reader,options);
            }
        }

        internal static string GetEncodedXMLName(string name)
        {
            try
            {
                return XmlConvert.VerifyName(name);
            }
            catch (XmlException)
            {
                return XmlConvert.EncodeLocalName(name);
            }
        }
        /// <summary>
        /// Creates an instance of System.Xml.XmlReaderSettings with safe settings
        /// </summary>
        internal static XmlReaderSettings GetXmlReaderSettings(LoadOptions options)
        {
            XmlReaderSettings rs = new XmlReaderSettings();
            if ((options & LoadOptions.PreserveWhitespace) == 0)
                rs.IgnoreWhitespace = true;
            rs.IgnoreProcessingInstructions = true;
            return rs;
        }
    }
}
