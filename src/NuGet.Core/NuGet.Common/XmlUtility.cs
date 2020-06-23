// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Common
{
    public static class XmlUtility
    {
        /// <summary>
        /// Creates a new System.Xml.Linq.XDocument from a file.
        /// </summary>
        /// <param name="filePath">A URI string that references the file to load into a new <see cref="System.Xml.Linq.XDocument"/></param>
        /// <returns>An <see cref="System.Xml.Linq.XDocument"/> that contains the contents of the specified file.</returns>
        public static XDocument Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException(Strings.ArgumentNullOrEmpty, nameof(filePath));
            }

            using (var reader = XmlReader.Create(filePath, GetXmlReaderSettings()))
            {
                return XDocument.Load(reader);
            }
        }

        /// <summary>
        /// Creates an instance of System.Xml.XmlReaderSettings with safe settings
        /// </summary>
        private static XmlReaderSettings GetXmlReaderSettings()
        {
            return new XmlReaderSettings()
            {
                IgnoreWhitespace = true,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true
            };
        }
    }
}
