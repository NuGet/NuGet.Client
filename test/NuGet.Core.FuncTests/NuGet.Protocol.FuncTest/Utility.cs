// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NuGet.Common;

namespace NuGet.Protocol.FuncTest
{
    public static class Utility
    {
        public static Tuple<string, string> ReadCredential(string feedName)
        {
            string fullPath = NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);
            using (Stream configStream = File.OpenRead(Path.Combine(fullPath, "NuGet.Protocol.FuncTest.config")))
            {
                var doc = XDocument.Load(XmlReader.Create(configStream));
                var element = doc.Root.Element(feedName);

                if (element != null)
                {
                    return new Tuple<string, string>(element.Element("Username").Value, element.Element("Password").Value);
                }
            }

            return null;
        }
    }
}
