// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    public static class XmlUtility
    {
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

        private static XmlReaderSettings CreateSafeSettings(bool ignoreWhiteSpace = false)
        {
            var safeSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = ignoreWhiteSpace
            };

            return safeSettings;
        }
    }
}

